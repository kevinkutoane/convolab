using System.Reflection;
using System.Security.Claims;
using System.Data;
using System.Globalization;
using ConvoLab.Api.Health;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Operations.Backups;
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
    IBackupEvidenceSource backupEvidence,
    OperationalReadinessSummary readinessSummary,
    HealthCheckService healthChecks,
    IOptions<AuthenticationOptions> authenticationOptions,
    EntraDependencyEvidence entraEvidence,
    IOptions<OperationsThresholdOptions> thresholdOptions,
    IOptions<BuildOptions> buildOptions,
    IHostEnvironment environment,
    ILogger<OperationsController> logger,
    IServiceProvider serviceProvider) : ControllerBase
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
        var since = now.AddHours(-24);
        var configured = authenticationOptions.Value;
        var localLoginEnabled = configured.Mode == ConvoLabAuthenticationMode.Local
                                || configured.Mode == ConvoLabAuthenticationMode.Hybrid
                                && configured.Local.Enabled;
        var entraEnabled = configured.Entra.Enabled
                           && configured.Mode is ConvoLabAuthenticationMode.Entra or ConvoLabAuthenticationMode.Hybrid;
        var tenantConfigurationState = string.IsNullOrWhiteSpace(configured.Entra.TenantId)
            ? "NotConfigured"
            : "Configured";
        var clientAuthenticationScheme = ClientAuthenticationScheme(configured.Entra.ClientSecretReference);

        var aggregates = await ReadAuthenticationAggregatesAsync(since, now, ct);
        var breakGlassEnabled = configured.Local.BreakGlassEnabled;
        var breakGlassState = !breakGlassEnabled ? "Disabled"
            : aggregates.AuthorisedCredentialCount == 0 ? "Unavailable"
            : aggregates.AvailableCredentialCount > 0 ? "Available"
            : "Locked";
        var dependency = entraEvidence.Snapshot();
        return Ok(new
        {
            mode = configured.Mode.ToString(),
            localLoginEnabled,
            entraEnabled,
            tenantConfigurationState,
            clientAuthentication = new
            {
                configured = clientAuthenticationScheme is not null,
                secretProviderScheme = clientAuthenticationScheme
            },
            state = dependency.State,
            lastValidationAt = dependency.CheckedAt,
            lastFailureCode = dependency.FailureCode,
            externalIdentityCount = aggregates.ExternalIdentityCount,
            linkedActiveUsers = aggregates.LinkedActiveUsers,
            externalLoginSuccessesLast24Hours = aggregates.ExternalLoginSuccesses,
            externalLoginFailuresLast24Hours = aggregates.ExternalLoginFailures,
            activeSessions = aggregates.ActiveSessions,
            breakGlassEnabled,
            breakGlassAvailable = breakGlassState == "Available",
            breakGlassState,
            breakGlassUsesLast24Hours = aggregates.BreakGlassUses,
            breakGlassFailuresLast24Hours = aggregates.BreakGlassFailures,
            lastBreakGlassSuccessfulUseAt = aggregates.LastBreakGlassSuccess,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    private static string? ClientAuthenticationScheme(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var separator = reference.IndexOf(':');
        if (separator <= 0) return null;
        var scheme = reference[..separator].Trim().ToLowerInvariant();
        return scheme is "env" or "docker-secret" or "azure-key-vault" ? scheme : null;
    }

    private async Task<AuthenticationAggregates> ReadAuthenticationAggregatesAsync(
        DateTimeOffset since,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Explicit relational COUNT/MAX queries keep all audit/session aggregation in the
        // database and avoid SQLite's inability to translate DateTimeOffset aggregates.
        var connection = db.Database.GetDbConnection();
        var closeWhenComplete = connection.State != ConnectionState.Open;
        if (closeWhenComplete) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM "ExternalIdentities"),
                    (SELECT COUNT(DISTINCT identity."UserId")
                       FROM "ExternalIdentities" AS identity
                       INNER JOIN "IdentityUsers" AS linkedUser ON linkedUser."Id" = identity."UserId"
                      WHERE identity."IsActive" = TRUE AND linkedUser."Status" = 'Active'),
                    (SELECT COUNT(*) FROM "WorkspaceAuditEvents"
                      WHERE "Action" = 'Authentication.EntraLogin'
                        AND "Outcome" = 'Succeeded' AND "OccurredAt" >= @since),
                    (SELECT COUNT(*) FROM "WorkspaceAuditEvents"
                      WHERE "Action" = 'Authentication.EntraLogin'
                        AND "Outcome" <> 'Succeeded' AND "OccurredAt" >= @since),
                    (SELECT COUNT(*) FROM "AuthenticationSessions"
                      WHERE "RevokedAt" IS NULL AND "ExpiresAt" > @now),
                    (SELECT COUNT(*) FROM "WorkspaceAuditEvents"
                      WHERE "Action" = 'Authentication.BreakGlassLogin'
                        AND "Outcome" = 'Succeeded' AND "OccurredAt" >= @since),
                    (SELECT COUNT(*) FROM "WorkspaceAuditEvents"
                      WHERE "Action" = 'Authentication.BreakGlassFailure' AND "OccurredAt" >= @since),
                    (SELECT MAX("OccurredAt") FROM "WorkspaceAuditEvents"
                      WHERE "Action" = 'Authentication.BreakGlassLogin' AND "Outcome" = 'Succeeded'),
                    (SELECT COUNT(*) FROM "IdentityUsers" AS administrator
                       INNER JOIN "LocalCredentials" AS credential ON credential."UserId" = administrator."Id"
                      WHERE administrator."Status" = 'Active' AND administrator."IsPlatformAdministrator" = TRUE),
                    (SELECT COUNT(*) FROM "IdentityUsers" AS administrator
                       INNER JOIN "LocalCredentials" AS credential ON credential."UserId" = administrator."Id"
                      WHERE administrator."Status" = 'Active' AND administrator."IsPlatformAdministrator" = TRUE
                        AND (credential."BreakGlassLockedUntil" IS NULL OR credential."BreakGlassLockedUntil" <= @now))
                """;
            var sinceParameter = command.CreateParameter();
            sinceParameter.ParameterName = "@since";
            sinceParameter.Value = since;
            command.Parameters.Add(sinceParameter);
            var nowParameter = command.CreateParameter();
            nowParameter.ParameterName = "@now";
            nowParameter.Value = now;
            command.Parameters.Add(nowParameter);
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new AuthenticationAggregates(
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture),
                ParseTimestamp(reader.GetValue(7)),
                Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture));
        }
        finally
        {
            if (closeWhenComplete) await connection.CloseAsync();
        }
    }

    private static DateTimeOffset? ParseTimestamp(object value)
    {
        if (value is null or DBNull) return null;
        if (value is DateTimeOffset timestamp) return timestamp;
        if (value is DateTime dateTime) return new DateTimeOffset(dateTime);
        return DateTimeOffset.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    private sealed record AuthenticationAggregates(
        int ExternalIdentityCount,
        int LinkedActiveUsers,
        int ExternalLoginSuccesses,
        int ExternalLoginFailures,
        int ActiveSessions,
        int BreakGlassUses,
        int BreakGlassFailures,
        DateTimeOffset? LastBreakGlassSuccess,
        int AuthorisedCredentialCount,
        int AvailableCredentialCount);

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
    public ActionResult Backups()
    {
        var evidence = backupEvidence.Snapshot();
        return Ok(new
        {
            state = evidence.State,
            message = evidence.Message,
            lastBackupCompletedAt = evidence.LastBackupCompletedAt,
            lastBackupVerifiedAt = evidence.LastBackupVerifiedAt,
            lastBackupSizeBytes = evidence.LastBackupSizeBytes,
            configuredRpo = evidence.ConfiguredRpo,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("backups")]
    public async Task<ActionResult> CreateBackup(CancellationToken ct)
    {
        var executor = serviceProvider.GetService<IBackupExecutor>();
        if (executor == null) return StatusCode(StatusCodes.Status501NotImplemented, "Backup execution is not configured in this environment.");
        var artifact = await executor.ExecuteBackupAsync(ct);
        return Ok(artifact);
    }

    [HttpPost("backups/{id}/verify")]
    public async Task<ActionResult> VerifyBackup(string id, CancellationToken ct)
    {
        var verifier = serviceProvider.GetService<IRecoveryVerifier>();
        if (verifier == null) return StatusCode(StatusCodes.Status501NotImplemented, "Recovery verification is not configured in this environment.");
        var result = await verifier.VerifyRecoveryAsync(ct);
        return Ok(result);
    }

    [HttpPost("backups/{id}/restore")]
    public async Task<ActionResult> RestoreBackup(string id, [FromQuery] bool allowDestructive = false, CancellationToken ct = default)
    {
        var executor = serviceProvider.GetService<IRestoreExecutor>();
        if (executor == null) return StatusCode(StatusCodes.Status501NotImplemented, "Restore execution is not configured in this environment.");
        if (!allowDestructive)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "urn:convolab:operations:restore-destructive",
                Title = "Destructive Restore Required",
                Detail = "This environment is active. A restore will overwrite the database and document storage. You must explicitly set allowDestructive=true.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var options = new RestoreOptions(id, allowDestructive, SessionRecoveryMode.Invalidate);
        var operationId = await executor.EnqueueRestoreAsync(options, ct);

        return Accepted($"/api/operations/recovery/{operationId}", new { operationId });
    }

    [HttpGet("recovery/{operationId}")]
    public async Task<ActionResult> GetRecoveryStatus(Guid operationId, CancellationToken ct)
    {
        var executor = serviceProvider.GetService<IRestoreExecutor>();
        if (executor == null) return StatusCode(StatusCodes.Status501NotImplemented, "Restore execution is not configured in this environment.");
        var status = await executor.GetRestoreStatusAsync(operationId, ct);
        if (status == null) return NotFound();
        return Ok(status);
    }

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
