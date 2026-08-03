using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConvoLab.Application.Operations;

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
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AuthenticationLogins =
        Meter.CreateCounter<long>("convolab.auth.login.total");
    public static readonly Counter<long> AuthenticationFailures =
        Meter.CreateCounter<long>("convolab.auth.login.failure.total");
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
    public static readonly UpDownCounter<long> ActiveSessions =
        Meter.CreateUpDownCounter<long>("convolab.auth.session.active");
    public static readonly Counter<long> OperationalStatusReads =
        Meter.CreateCounter<long>("convolab.operations.status.read.total");
}

public static class SensitiveTelemetryHttpRequestOptions
{
    public static readonly HttpRequestOptionsKey<bool> SuppressAutomaticInstrumentation =
        new("ConvoLab.SuppressAutomaticHttpInstrumentation");
}
