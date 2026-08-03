using System.Reflection;
using System.Security.Claims;
using ConvoLab.Application.Operations;
using ConvoLab.Domain.Analytics;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAdministrator")]
[Route("api/operations")]
public sealed class OperationsController(
    ApplicationDbContext db,
    IPlatformOperationalState operationalState,
    IPlatformOperationalAdministration administration,
    ISecretProviderEvidenceSource secretEvidence,
    HealthCheckService healthChecks,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<OperationsController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> Status(CancellationToken ct)
    {
        ConvoLabTelemetry.OperationalStatusReads.Add(1);
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.status.read");
        var safeMode = await operationalState.GetAsync(ct);
        var workerRows = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .Select(item => new { item.LastHeartbeatAt, item.CurrentStatus })
            .ToListAsync(ct);
        var worker = workerRows.OrderByDescending(item => item.LastHeartbeatAt).FirstOrDefault();
        var pendingOutbox = await db.AnalyticsOutbox.AsNoTracking()
            .CountAsync(item => item.Status == "Pending", ct);
        var workerAge = worker is null ? (double?)null : (DateTimeOffset.UtcNow - worker.LastHeartbeatAt).TotalSeconds;
        var overall = workerAge > 60 ? "Unhealthy"
            : pendingOutbox > 0 || worker is null || worker.CurrentStatus == "Degraded" ? "Degraded"
            : "Healthy";
        logger.LogInformation("Operational summary read {EventName} {Outcome}", "Operations.StatusRead", overall);
        return Ok(new
        {
            status = overall,
            version = Version(),
            workstream = "alpha.15-operational-foundation",
            releaseStatus = "in-progress",
            environment = environment.EnvironmentName,
            safeMode,
            telemetry = TelemetryState(),
            worker = new { state = WorkerState(worker, workerAge), staleAfterSeconds = 60 },
            analytics = new { pendingOutbox },
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("readiness")]
    public async Task<ActionResult> Readiness(CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.readiness.open");
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"), ct);
        var audit = AuthController.Audit(
            "Platform", null, null, User.FindFirstValue("actor_type") ?? "User", ActorId(),
            User.Identity?.Name ?? "Platform administrator", "Operations.ReadinessEvidenceViewed",
            "OperationalReadiness", null, "Succeeded", HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(audit);
        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            status = report.Status.ToString(),
            version = Version(),
            thresholds = new
            {
                workerStaleSeconds = 60,
                outboxWarningSeconds = 60,
                outboxUnhealthySeconds = 300,
                aggregationWarningSeconds = 120,
                aggregationUnhealthySeconds = 600
            },
            components = report.Entries.Select(entry => new
            {
                component = entry.Key,
                state = State(entry.Key, entry.Value),
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds
            }),
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("workers")]
    public async Task<ActionResult> Workers(CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.workers.read");
        var workers = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .OrderBy(item => item.WorkerName)
            .Select(item => new
            {
                item.WorkerName, item.InstanceId, item.StartedAt, item.LastHeartbeatAt,
                item.LastSuccessfulIterationAt, item.LastFailureAt, item.LastFailureSummary,
                item.CurrentStatus, item.ProcessedCount, item.LeaseExpiresAt, item.Revision
            }).ToListAsync(ct);
        return Ok(new { workers, staleAfterSeconds = 60, correlationId = HttpContext.TraceIdentifier });
    }

    [HttpGet("analytics-pipeline")]
    public async Task<ActionResult> AnalyticsPipeline(CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.analytics_pipeline.read");
        var now = DateTimeOffset.UtcNow;
        List<OutboxStatusSummary> outbox;
        if (db.Database.IsSqlite())
        {
            var rows = await db.AnalyticsOutbox.AsNoTracking()
                .Select(item => new { item.Status, item.CreatedAt })
                .ToListAsync(ct);
            outbox = rows.GroupBy(item => item.Status)
                .Select(group => new OutboxStatusSummary(
                    group.Key, group.Count(), group.Min(item => item.CreatedAt)))
                .ToList();
        }
        else
        {
            outbox = await db.AnalyticsOutbox.AsNoTracking()
                .GroupBy(item => item.Status)
                .Select(group => new OutboxStatusSummary(
                    group.Key, group.Count(), group.Min(item => item.CreatedAt)))
                .ToListAsync(ct);
        }
        var checkpoints = await db.AnalyticsAggregationCheckpoints.AsNoTracking()
            .Select(item => new
            {
                item.Granularity, item.Status, item.HighWatermarkUtc, item.DirtyFromUtc,
                item.LastSuccessfulRunAt, item.UpdatedAt
            }).ToListAsync(ct);
        return Ok(new
        {
            outbox = outbox.Select(item => new
            {
                status = item.Status,
                count = item.Count,
                oldestAgeSeconds = Math.Max(0, (now - item.Oldest).TotalSeconds)
            }),
            checkpoints,
            thresholds = new { outboxWarningSeconds = 60, outboxUnhealthySeconds = 300, aggregationWarningSeconds = 120, aggregationUnhealthySeconds = 600 },
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("authentication")]
    public async Task<ActionResult> Authentication(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var activeSessions = await db.AuthenticationSessions.AsNoTracking()
            .CountAsync(item => item.RevokedAt == null && item.ExpiresAt > now, ct);
        var recentFailures = await db.WorkspaceAuditEvents.AsNoTracking()
            .CountAsync(item => item.Action == "Authentication.Login" && item.Outcome == "Failed" && item.OccurredAt >= now.AddHours(-24), ct);
        return Ok(new
        {
            mode = configuration["Authentication:Mode"] ?? "Local",
            state = OperationalDependencyState.Configured,
            activeSessions,
            failuresLast24Hours = recentFailures,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("secret-providers")]
    public ActionResult SecretProviders() => Ok(new
    {
        providers = secretEvidence.Snapshot(),
        correlationId = HttpContext.TraceIdentifier
    });

    [HttpGet("backups")]
    public ActionResult Backups() => Ok(new
    {
        state = OperationalDependencyState.NotConfigured,
        message = "Backup and restore tooling is deferred to a later alpha.15 workstream.",
        correlationId = HttpContext.TraceIdentifier
    });

    [HttpGet("build")]
    public ActionResult Build() => Ok(new
    {
        version = Version(),
        workstream = "alpha.15-operational-foundation",
        releaseStatus = "in-progress",
        commit = configuration["Build:Commit"],
        buildTime = configuration.GetValue<DateTimeOffset?>("Build:Time"),
        digests = configuration.GetSection("Build:Digests").Get<Dictionary<string, string>>() ?? [],
        correlationId = HttpContext.TraceIdentifier
    });

    [HttpPost("safe-mode")]
    public async Task<ActionResult<PlatformOperationalState>> UpdateSafeMode(
        SafeModeRequest request,
        CancellationToken ct)
    {
        var result = await administration.UpdateSafeModeAsync(new(
            request.Enabled, request.ExpectedRevision, request.Reason ?? string.Empty,
            request.Confirmation ?? string.Empty, ActorId() ?? Guid.Empty,
            User.Identity?.Name ?? "Platform administrator",
            ClaimGuid("organisation_id"), ClaimGuid("workspace_id"),
            HttpContext.TraceIdentifier), ct);
        return Ok(result);
    }

    private string TelemetryState()
    {
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                       ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var traces = configuration["OTEL_TRACES_EXPORTER"]
                     ?? Environment.GetEnvironmentVariable("OTEL_TRACES_EXPORTER");
        var metrics = configuration["OTEL_METRICS_EXPORTER"]
                      ?? Environment.GetEnvironmentVariable("OTEL_METRICS_EXPORTER");
        var tracesEnabled = IncludesOtlp(traces)
                            || (string.IsNullOrWhiteSpace(traces)
                                && !string.IsNullOrWhiteSpace(endpoint));
        var metricsEnabled = IncludesOtlp(metrics)
                             || (string.IsNullOrWhiteSpace(metrics)
                                 && !string.IsNullOrWhiteSpace(endpoint));
        return tracesEnabled || metricsEnabled
            ? OperationalDependencyState.Configured.ToString()
            : OperationalDependencyState.NotConfigured.ToString();
    }

    private static bool IncludesOtlp(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => item.Equals("otlp", StringComparison.OrdinalIgnoreCase)) == true;

    private static OperationalDependencyState WorkerState(object? worker, double? age) =>
        worker is null ? OperationalDependencyState.Configured
        : age > 60 ? OperationalDependencyState.Unavailable
        : OperationalDependencyState.LiveValidated;

    private static OperationalDependencyState State(string component, HealthReportEntry entry)
    {
        if (entry.Status == HealthStatus.Unhealthy) return OperationalDependencyState.Unavailable;
        if (entry.Status == HealthStatus.Degraded) return OperationalDependencyState.Degraded;
        if (component == "providers")
            return OperationalDependencyState.StubValidated;
        if (component == "production-configuration")
            return OperationalDependencyState.Configured;
        if (component == "required-secrets"
            && entry.Data.TryGetValue("configuredReferences", out var count)
            && Convert.ToInt32(count) == 0)
            return OperationalDependencyState.NotConfigured;
        return OperationalDependencyState.LiveValidated;
    }

    private Guid? ActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private Guid? ClaimGuid(string claimType) =>
        Guid.TryParse(User.FindFirstValue(claimType), out var id) ? id : null;

    private static string Version() =>
        (typeof(OperationsController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? typeof(OperationsController).Assembly.GetName().Version?.ToString()
         ?? "unknown").Split('+', 2)[0];

    private sealed record OutboxStatusSummary(
        string Status,
        int Count,
        DateTimeOffset Oldest);
}

public sealed record SafeModeRequest(
    bool Enabled,
    long ExpectedRevision,
    string? Reason,
    string? Confirmation);
