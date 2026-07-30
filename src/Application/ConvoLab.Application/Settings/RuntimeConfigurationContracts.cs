using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Settings;

namespace ConvoLab.Application.Settings;

/// <summary>
/// Immutable, typed view of the complete governed configuration used by one
/// execution. SecretReference is a reference only; secret material is resolved
/// by the provider immediately before invocation.
/// </summary>
public sealed record RuntimeExecutionConfiguration(
    ConfigurationSnapshot Snapshot,
    string Provider,
    string Model,
    string? SecretReference,
    double Temperature,
    int MaximumOutputTokens,
    int RequestTimeoutSeconds,
    int RetryCount,
    decimal MonthlyBudgetZar,
    decimal BudgetWarningThreshold,
    decimal BudgetHardStopThreshold,
    decimal? InputPriceZarPer1K,
    decimal? OutputPriceZarPer1K,
    bool AllowExecutionWhenPricingUnknown,
    double MinimumGroundedness,
    double MinimumRelevance,
    double MinimumSafety,
    double MinimumOverall,
    string EvaluationFailureAction,
    bool PolicyEnforcementEnabled,
    string PolicyDenialBehaviour,
    bool ProviderExecutionEnabled,
    bool ReplayExecutionEnabled,
    bool PluginActivationEnabled,
    bool SensitiveTraceRevealEnabled,
    string TraceRedactionLevel,
    IReadOnlyDictionary<string, string?> PluginDefaults,
    IReadOnlyDictionary<string, string?> FeatureFlags);

public sealed record RuntimeExecutionOverrides(
    string? Provider = null,
    string? Model = null,
    double? Temperature = null,
    int? MaximumOutputTokens = null);

public interface IRuntimeConfigurationResolver
{
    Task<RuntimeExecutionConfiguration> ResolveAsync(
        IRuntimeRequestContext context,
        RuntimeExecutionOverrides? overrides = null,
        CancellationToken cancellationToken = default);
}
