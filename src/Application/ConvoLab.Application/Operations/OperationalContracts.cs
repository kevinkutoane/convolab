using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConvoLab.Application.Operations;

public static class OperationalWorkstream
{
    public const string Label = "alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication";
    public const string Marker = "alpha.15-entra-hybrid-authentication";
}

public enum OperationalDependencyState
{
    NotConfigured,
    Configured,
    StubValidated,
    LiveValidated,
    Unavailable,
    Degraded
}

public sealed record ProductionReadinessFinding(
    string Code,
    string Severity,
    string ConfigurationKey,
    string Message);

public sealed record ProductionReadinessResult(
    bool IsReady,
    IReadOnlyList<ProductionReadinessFinding> Findings);

public interface IProductionReadinessValidator
{
    Task<ProductionReadinessResult> ValidateAsync(CancellationToken ct = default);
}

public sealed record SecretProviderEvidence(
    string Provider,
    OperationalDependencyState State,
    DateTimeOffset? LastCheckedAt,
    string? LastErrorCode);

public interface ISecretProviderEvidenceSource
{
    IReadOnlyList<SecretProviderEvidence> Snapshot();
}

public sealed record RequiredSecretEvidence(
    Guid EnvironmentId,
    string EnvironmentName,
    string Provider,
    string? SecretProviderScheme,
    bool Required,
    OperationalDependencyState DependencyState,
    string? FailureCode,
    DateTimeOffset? LastValidatedAt);

public sealed record RequiredSecretReadinessSnapshot(
    IReadOnlyList<RequiredSecretEvidence> Environments,
    IReadOnlyList<string> ScopeFailureCodes);

public interface IRequiredSecretReadinessEvaluator
{
    Task<RequiredSecretReadinessSnapshot> EvaluateAsync(
        CancellationToken ct = default);
}

public sealed record TelemetryDependencyEvidence(
    OperationalDependencyState State,
    bool EndpointConfigured,
    bool TraceExportEnabled,
    bool MetricExportEnabled,
    string ServiceName,
    DateTimeOffset? LastLiveValidatedAt,
    string? LastFailureCode);

public interface ITelemetryDependencyEvidenceSource
{
    TelemetryDependencyEvidence Snapshot();
}

public sealed record AnalyticsMaintenanceResult(
    int OutboxProcessed,
    int OutboxFailed,
    int ExportsCompleted,
    int ExportsFailed,
    int AggregateBucketsCompleted,
    int AggregateBucketsFailed,
    int RetentionRowsRemoved,
    bool PartialFailure,
    IReadOnlyCollection<string> FailureCodes)
{
    public long TotalProcessed =>
        (long)OutboxProcessed
        + ExportsCompleted
        + AggregateBucketsCompleted
        + RetentionRowsRemoved;

    public static AnalyticsMaintenanceResult Empty { get; } = new(
        0, 0, 0, 0, 0, 0, 0, false, []);
}

public sealed record PlatformOperationalState(
    bool PersistedSafeModeEnabled,
    bool EnvironmentOverrideEnabled,
    bool EffectiveSafeModeEnabled,
    bool AllowDeterministicVerification,
    bool? BlockAnalyticsExports,
    string? Reason,
    long Revision,
    DateTimeOffset ChangedAt);

public interface IPlatformOperationalState
{
    Task<PlatformOperationalState> GetAsync(CancellationToken ct = default);
    Task EnsureExternalExecutionAllowedAsync(CancellationToken ct = default);
    Task EnsureDeterministicExecutionAllowedAsync(CancellationToken ct = default);
    Task EnsureAnalyticsExportAllowedAsync(CancellationToken ct = default);
}

public sealed record UpdateSafeModeCommand(
    bool Enabled,
    long ExpectedRevision,
    string Reason,
    string Confirmation,
    Guid ActorId,
    string ActorDisplay,
    Guid? OrganisationId,
    Guid? WorkspaceId,
    string CorrelationId);

public interface IPlatformOperationalAdministration
{
    Task<PlatformOperationalState> UpdateSafeModeAsync(
        UpdateSafeModeCommand command,
        CancellationToken ct = default);
}

public static class ConvoLabTelemetry
{
    public const string SourceName = "ConvoLab.OperationalFoundation";
    public const string MeterName = "ConvoLab.OperationalFoundation";
    public const string DatabaseMeterName = "ConvoLab.OperationalFoundation.DatabaseState";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AuthenticationLogins =
        Meter.CreateCounter<long>("convolab.auth.login.total");
    public static readonly Counter<long> AuthenticationFailures =
        Meter.CreateCounter<long>("convolab.auth.login.failure.total");
    public static readonly Counter<long> EntraChallenges =
        Meter.CreateCounter<long>("convolab.auth.entra.challenge.total");
    public static readonly Counter<long> EntraLoginSuccesses =
        Meter.CreateCounter<long>("convolab.auth.entra.login.success.total");
    public static readonly Counter<long> EntraLoginFailures =
        Meter.CreateCounter<long>("convolab.auth.entra.login.failure.total");
    public static readonly Counter<long> ExternalIdentityLinks =
        Meter.CreateCounter<long>("convolab.auth.external_identity.link.total");
    public static readonly Counter<long> ExternalIdentityUnlinked =
        Meter.CreateCounter<long>("convolab.auth.external_identity.unlinked.total");
    public static readonly Counter<long> BreakGlassLogins =
        Meter.CreateCounter<long>("convolab.auth.break_glass.total");
    public static readonly Counter<long> AuthenticationLogouts =
        Meter.CreateCounter<long>("convolab.auth.logout.total");
    public static readonly Counter<long> PolicyDecisions =
        Meter.CreateCounter<long>("convolab.policy.decision.total");
    public static readonly Counter<long> ProviderInvocations =
        Meter.CreateCounter<long>("convolab.provider.invocation.total");
    public static readonly Histogram<double> ProviderDuration =
        Meter.CreateHistogram<double>("convolab.provider.invocation.duration", "ms");
    public static readonly Counter<long> ProviderFailures =
        Meter.CreateCounter<long>("convolab.provider.invocation.failure.total");
    public static readonly Counter<long> ProviderInputTokens =
        Meter.CreateCounter<long>("convolab.provider.tokens.input");
    public static readonly Counter<long> ProviderOutputTokens =
        Meter.CreateCounter<long>("convolab.provider.tokens.output");
    public static readonly Counter<double> ProviderCostZar =
        Meter.CreateCounter<double>("convolab.provider.cost.zar", "ZAR");
    public static readonly Histogram<double> AnalyticsWorkerDuration =
        Meter.CreateHistogram<double>("convolab.analytics.worker.duration", "ms");
    public static readonly Counter<long> AnalyticsWorkerFailures =
        Meter.CreateCounter<long>("convolab.analytics.worker.failure");
    public static readonly Counter<long> AnalyticsOutboxProcessed =
        Meter.CreateCounter<long>("convolab.analytics.outbox.processed.total");
    public static readonly Counter<long> AnalyticsAggregationRuns =
        Meter.CreateCounter<long>("convolab.analytics.aggregation.run.total");
    public static readonly Counter<long> SecretResolutionFailures =
        Meter.CreateCounter<long>("convolab.secret.resolve.failure");
    public static readonly Counter<long> SafeModeBlocks =
        Meter.CreateCounter<long>("convolab.safe_mode.blocked.total");
    public static readonly Counter<long> SafeModeChanges =
        Meter.CreateCounter<long>("convolab.safe_mode.change.total");
    public static readonly Counter<long> OperationalStatusReads =
        Meter.CreateCounter<long>("convolab.operations.status.read.total");
}

public static class SensitiveTelemetryHttpRequestOptions
{
    public static readonly HttpRequestOptionsKey<bool> SuppressAutomaticInstrumentation =
        new("ConvoLab.SuppressAutomaticHttpInstrumentation");

    public static bool ShouldInstrument(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(
                SuppressAutomaticInstrumentation,
                out var suppress) && suppress)
            return false;

        var host = request.RequestUri?.Host ?? string.Empty;
        return !host.Equals(
                   "generativelanguage.googleapis.com",
                   StringComparison.OrdinalIgnoreCase)
               && !host.EndsWith(
                   ".vault.azure.net",
                   StringComparison.OrdinalIgnoreCase);
    }
}
