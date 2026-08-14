using System.Reflection;
using System.Security.Claims;
using ConvoLab.Api.Health;
using ConvoLab.Application.Operations;
using ConvoLab.Domain.Analytics;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ConvoLab.Api.Security;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAdministrator")]
[Route("api/operations")]
public sealed class OperationsController(
    ApplicationDbContext db,
    IPlatformOperationalState operationalState,
    IPlatformOperationalAdministration administration,
    ISecretProviderEvidenceSource secretEvidence,
    IRequiredSecretReadinessEvaluator requiredSecrets,
    ITelemetryDependencyEvidenceSource telemetryEvidence,
    IAnalyticsOperationalEvidenceReader analyticsEvidence,
    OperationalReadinessSummary readinessSummary,
    HealthCheckService healthChecks,
    IConfiguration configuration,
    IOptions<OperationsThresholdOptions> thresholdOptions,
    IOptions<BuildOptions> buildOptions,
    EntraDependencyEvidence entraEvidence,
    IHostEnvironment environment,
    ILogger<OperationsController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> Status(CancellationToken ct)
    {
        ConvoLabTelemetry.OperationalStatusReads.Add(1);
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.status.read");
        var safeMode = await operationalState.GetAsync(ct);
        var worker = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .Where(item => item.WorkerName == "analytics-maintenance")
            .Select(item => new { item.LastHeartbeatAt, item.CurrentStatus })
            .SingleOrDefaultAsync(ct);
        var pipeline = await analyticsEvidence.ReadAsync(ct);
        var pipelineStatus = AnalyticsPipelineStatusEvaluator.Evaluate(
            pipeline,
            thresholdOptions.Value,
            worker?.CurrentStatus == "Degraded");
        var readiness = readinessSummary.Snapshot();
        var workerAge = worker is null ? (double?)null : (DateTimeOffset.UtcNow - worker.LastHeartbeatAt).TotalSeconds;
        var overall = readiness.Status == "Unhealthy"
                      || workerAge >= thresholdOptions.Value.WorkerUnhealthySeconds
                      || worker?.CurrentStatus is "Failed" or "LeaseLost"
                      || pipelineStatus == OperationalStatusLevel.Unhealthy
            ? "Unhealthy"
            : readiness.Status is "Degraded" or "Unknown"
              || worker is null
              || workerAge >= thresholdOptions.Value.WorkerWarningSeconds
              || worker.CurrentStatus == "Degraded"
              || pipelineStatus == OperationalStatusLevel.Degraded
                ? "Degraded"
                : "Healthy";
        logger.LogInformation("Operational summary read {EventName} {Outcome}", "Operations.StatusRead", overall);
        return Ok(new
        {
            status = overall,
            version = Version(),
            workstream = OperationalWorkstream.Label,
            releaseStatus = "in-progress",
            environment = environment.EnvironmentName,
            readiness,
            safeMode,
            telemetry = telemetryEvidence.Snapshot().State,
            worker = new
            {
                state = WorkerState(worker, workerAge, thresholdOptions.Value),
                warningAfterSeconds = thresholdOptions.Value.WorkerWarningSeconds,
                staleAfterSeconds = thresholdOptions.Value.WorkerUnhealthySeconds
            },
            analytics = new
            {
                pipeline.PendingCount,
                pipeline.FailedCount,
                status = pipelineStatus.ToString()
            },
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("readiness")]
    public async Task<ActionResult> Readiness(CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.readiness.open");
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"), ct);
        readinessSummary.Record(report.Status);
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
            thresholds = thresholdOptions.Value,
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
                item.WorkerName,
                leaseOwner = item.InstanceId,
                leaseToken = item.LeaseToken,
                item.StartedAt,
                item.LastHeartbeatAt,
                item.LastIterationStartedAt,
                item.LastIterationCompletedAt,
                item.LastSuccessfulIterationAt,
                item.LastDegradedIterationAt,
                item.LastFailureAt,
                item.LastFailureCode,
                item.LastFailureSummary,
                item.CurrentStatus,
                item.LastOutboxProcessed,
                item.LastOutboxFailed,
                item.LastExportsCompleted,
                item.LastExportsFailed,
                item.LastAggregateBucketsCompleted,
                item.LastAggregateBucketsFailed,
                item.LastRetentionRowsRemoved,
                item.CumulativeProcessedCount,
                item.LeaseExpiresAt,
                item.Revision
            }).ToListAsync(ct);
        return Ok(new
        {
            workers,
            warningAfterSeconds = thresholdOptions.Value.WorkerWarningSeconds,
            staleAfterSeconds = thresholdOptions.Value.WorkerUnhealthySeconds,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("analytics-pipeline")]
    public async Task<ActionResult> AnalyticsPipeline(CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("operations.analytics_pipeline.read");
        var evidence = await analyticsEvidence.ReadAsync(ct);
        var partialWorkerFailure = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .AnyAsync(item => item.WorkerName == "analytics-maintenance"
                && item.CurrentStatus == "Degraded", ct);
        return Ok(new
        {
            evidence.PendingCount,
            evidence.FailedCount,
            evidence.OldestPendingAgeSeconds,
            evidence.OldestFailedAgeSeconds,
            evidence.AggregationDirtyCheckpointCount,
            evidence.AggregationFailedCheckpointCount,
            evidence.MaximumAggregationLagSeconds,
            evidence.LastSuccessfulOutboxDispatchAt,
            evidence.LastSuccessfulAggregationAt,
            status = AnalyticsPipelineStatusEvaluator.Evaluate(
                evidence,
                thresholdOptions.Value,
                partialWorkerFailure).ToString(),
            thresholds = thresholdOptions.Value,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("authentication")]
    public async Task<ActionResult> Authentication(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionExpiries = await db.AuthenticationSessions.AsNoTracking()
            .Where(item => item.RevokedAt == null).Select(item => item.ExpiresAt).ToListAsync(ct);
        var activeSessions = sessionExpiries.Count(expiresAt => expiresAt > now);
        var authenticationAudits = await db.WorkspaceAuditEvents.AsNoTracking()
            .Where(item => item.Action == "Authentication.Login" || item.Action == "Authentication.EntraLogin"
                           || item.Action == "Authentication.BreakGlassLogin")
            .Select(item => new { item.Action, item.Outcome, item.OccurredAt }).ToListAsync(ct);
        var since = now.AddHours(-24);
        var recentFailures = authenticationAudits.Count(item => item.Outcome == "Failed" && item.OccurredAt >= since);
        var externalIdentityCount = await db.ExternalIdentities.AsNoTracking().CountAsync(ct);
        var linkedActiveUsers = await db.ExternalIdentities.AsNoTracking().Where(item => item.IsActive)
            .Select(item => item.UserId).Distinct().CountAsync(ct);
        var externalSuccesses = authenticationAudits.Count(item => item.Action == "Authentication.EntraLogin"
            && item.Outcome == "Succeeded" && item.OccurredAt >= since);
        var breakGlassUses = authenticationAudits.Count(item => item.Action == "Authentication.BreakGlassLogin"
            && item.Outcome == "Succeeded" && item.OccurredAt >= since);
        var evidence = entraEvidence.Snapshot();
        var secretReference = configuration["Authentication:Entra:ClientSecretReference"] ?? string.Empty;
        var secretScheme = secretReference.Split(':', 2)[0];
        return Ok(new
        {
            mode = configuration["Authentication:Mode"] ?? "Local",
            localLoginEnabled = configuration.GetValue("Authentication:Local:Enabled", true),
            entraEnabled = configuration.GetValue<bool>("Authentication:Entra:Enabled"),
            breakGlassEnabled = configuration.GetValue<bool>("Authentication:Local:BreakGlassEnabled"),
            tenantConfigurationState = string.IsNullOrWhiteSpace(configuration["Authentication:Entra:TenantId"]) ? "NotConfigured" : "Configured",
            clientAuthentication = new { configured = !string.IsNullOrWhiteSpace(secretReference), secretProviderScheme = string.IsNullOrWhiteSpace(secretReference) ? null : secretScheme },
            state = evidence.State,
            lastValidationAt = evidence.CheckedAt,
            lastFailureCode = evidence.FailureCode,
            externalIdentityCount,
            linkedActiveUsers,
            externalLoginSuccessesLast24Hours = externalSuccesses,
            externalLoginFailuresLast24Hours = recentFailures,
            breakGlassUsesLast24Hours = breakGlassUses,
            activeSessions,
            failuresLast24Hours = recentFailures,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("secret-providers")]
    public async Task<ActionResult> SecretProviders(CancellationToken ct)
    {
        var required = await requiredSecrets.EvaluateAsync(ct);
        return Ok(new
        {
            providers = secretEvidence.Snapshot(),
            requiredEnvironments = required.Environments,
            scopeFailureCodes = required.ScopeFailureCodes,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("backups")]
    public ActionResult Backups() => Ok(new
    {
        state = OperationalDependencyState.NotConfigured,
        message = "Backup and restore tooling is deferred to a later alpha.15 workstream.",
        correlationId = HttpContext.TraceIdentifier
    });

    [HttpGet("telemetry")]
    public ActionResult Telemetry()
    {
        var evidence = telemetryEvidence.Snapshot();
        return Ok(new
        {
            otlpDependencyState = evidence.State,
            evidence.EndpointConfigured,
            evidence.TraceExportEnabled,
            evidence.MetricExportEnabled,
            evidence.ServiceName,
            releaseVersion = Version(),
            evidence.LastLiveValidatedAt,
            evidence.LastFailureCode,
            validationMethod = evidence.State == OperationalDependencyState.LiveValidated
                ? "Collector TCP connection succeeded; exporter delivery callbacks are not available."
                : "Configuration and explicit collector reachability probe only.",
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("build")]
    public ActionResult Build() => Ok(new
    {
        version = Version(),
        workstream = OperationalWorkstream.Label,
        releaseStatus = "in-progress",
        commit = buildOptions.Value.Commit,
        buildTime = buildOptions.Value.Time,
        digests = buildOptions.Value.Digests,
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

    private static OperationalDependencyState WorkerState(
        object? worker,
        double? age,
        OperationsThresholdOptions thresholds) =>
        worker is null ? OperationalDependencyState.Configured
        : age >= thresholds.WorkerUnhealthySeconds ? OperationalDependencyState.Unavailable
        : age >= thresholds.WorkerWarningSeconds ? OperationalDependencyState.Degraded
        : OperationalDependencyState.LiveValidated;

    private static OperationalDependencyState State(string component, HealthReportEntry entry)
    {
        if (entry.Status == HealthStatus.Unhealthy) return OperationalDependencyState.Unavailable;
        if (entry.Status == HealthStatus.Degraded) return OperationalDependencyState.Degraded;
        if (component == "providers")
            return OperationalDependencyState.StubValidated;
        if (component == "entra-authentication"
            && entry.Data.TryGetValue("state", out var entraState)
            && Enum.TryParse<OperationalDependencyState>(Convert.ToString(entraState), out var parsedEntraState))
            return parsedEntraState;
        if (component == "production-configuration")
            return OperationalDependencyState.Configured;
        if (component == "required-secrets"
            && entry.Data.TryGetValue("scopedEnvironments", out var count)
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

}

public sealed record SafeModeRequest(
    bool Enabled,
    long ExpectedRevision,
    string? Reason,
    string? Confirmation);
