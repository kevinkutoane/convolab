using System.Security.Cryptography;
using System.Text;

namespace ConvoLab.Domain.Analytics;

public enum AnalyticsCostType
{
    Actual,
    Estimated,
    Unavailable
}

public sealed record AnalyticsCost(decimal? AmountZar, AnalyticsCostType Type, string? PricingRevision)
{
    public static AnalyticsCost Classify(
        decimal? actualZar,
        int? inputTokens,
        int? outputTokens,
        decimal? inputPricePerThousand,
        decimal? outputPricePerThousand,
        string? pricingRevision,
        bool providerInvocationPrevented = false)
    {
        if (providerInvocationPrevented)
            return new AnalyticsCost(0m, AnalyticsCostType.Actual, "policy-prevented");
        if (actualZar.HasValue)
            return new AnalyticsCost(actualZar.Value, AnalyticsCostType.Actual, pricingRevision);
        if (inputTokens.HasValue && outputTokens.HasValue
            && inputPricePerThousand.HasValue && outputPricePerThousand.HasValue)
        {
            var estimate = inputTokens.Value / 1000m * inputPricePerThousand.Value
                + outputTokens.Value / 1000m * outputPricePerThousand.Value;
            return new AnalyticsCost(estimate, AnalyticsCostType.Estimated, pricingRevision);
        }
        return new AnalyticsCost(null, AnalyticsCostType.Unavailable, pricingRevision);
    }
}

public static class AnalyticsKeys
{
    public static string Event(string sourceType, Guid sourceId, string eventType) =>
        Hash($"{sourceType.Trim().ToLowerInvariant()}|{sourceId:N}|{eventType.Trim().ToLowerInvariant()}");

    public static string Aggregate(params string?[] dimensions) =>
        Hash(string.Join('|', dimensions.Select(value => value?.Trim().ToLowerInvariant() ?? "∅")));

    public static string ConfigurationRevision(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var canonical = string.Join('\n', values
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}\u001f{pair.Value ?? "null"}"));
        return $"sha256:{Hash(canonical)}";
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public static class AnalyticsSemantics
{
    private static readonly HashSet<string> ExecutionTerminalEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "SimulationCompleted", "SimulationFailed",
        "ReplayCompleted", "ReplayFailed"
    };

    public static bool IsExecutionTerminal(string eventType) =>
        ExecutionTerminalEvents.Contains(eventType);

    public static bool IsSimulationTerminal(string eventType) =>
        eventType is "SimulationCompleted" or "SimulationFailed";

    public static bool IsProviderInvocation(string eventType) =>
        eventType is "ProviderInvocationCompleted" or "ProviderInvocationFailed"
            or "ProviderInvocationTimedOut";

    public static bool IsEvaluationTerminal(string eventType) =>
        eventType is "EvaluationCompleted" or "EvaluationFailed";

    public static bool IsTraceTerminal(string eventType) =>
        eventType == "TraceCompleted";

    public static bool IsReplayTerminal(string eventType) =>
        eventType is "ReplayCompleted" or "ReplayFailed";

    public static bool IsPolicyEvaluation(string eventType) =>
        eventType == "PolicyEvaluated";

    public static bool IsPluginOperation(string eventType) =>
        eventType.StartsWith("Plugin", StringComparison.OrdinalIgnoreCase);
}
