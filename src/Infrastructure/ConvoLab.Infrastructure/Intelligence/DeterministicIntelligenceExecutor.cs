using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Infrastructure.Intelligence;

/// <summary>
/// Local development provider used by ConvoLab Studio's first functional
/// vertical slice. It returns repeatable grounded responses and supports
/// controllable retry and fallback scenarios without external API keys.
/// </summary>
public sealed class DeterministicIntelligenceExecutor : IIntelligenceExecutor
{
    public IReadOnlyCollection<ProviderKind> SupportedProviders { get; } =
        Enum.GetValues<ProviderKind>();

    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string renderedPrompt,
        CancellationToken cancellationToken = default)
    {
        var mode = ExtractMarker(renderedPrompt, "SIMULATION_MODE") ?? "Normal";

        if (mode.Equals("RetryOnce", StringComparison.OrdinalIgnoreCase) && request.AttemptNumber == 1)
            throw new TimeoutException("Deterministic retry scenario: the first attempt timed out.");

        if (mode.Equals("Fallback", StringComparison.OrdinalIgnoreCase)
            && request.Plan?.ModelName.Contains("Primary", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("Deterministic fallback scenario: primary model rejected the request.");

        await Task.Delay(
            request.Plan?.ModelName.Contains("Fallback", StringComparison.OrdinalIgnoreCase) == true ? 220 : 140,
            cancellationToken);

        var userMessage = ExtractSection(renderedPrompt, "USER MESSAGE:");
        var responseText = CreateGroundedResponse(userMessage);
        var usage = ExecutionUsage.Create(
            inputTokens: Math.Max(1, renderedPrompt.Length / 4),
            outputTokens: Math.Max(1, responseText.Length / 4));
        var cost = request.Plan?.ModelName.Contains("Fallback", StringComparison.OrdinalIgnoreCase) == true
            ? ExecutionCost.Create(0.008m, "ZAR")
            : ExecutionCost.Create(0.004m, "ZAR");

        return ExecutionResult.FromText(responseText, usage, cost);
    }

    private static string CreateGroundedResponse(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("hail") || lower.Contains("storm"))
        {
            return "Hail damage may be covered when your vehicle has comprehensive cover. " +
                   "The final decision depends on your policy schedule, exclusions, the applicable excess, and claim validation. " +
                   "Report the damage promptly, keep clear photographs, and avoid authorising repairs before the vehicle is assessed. " +
                   "Source: MiWay Motor Policy Wording — Hail and storm damage; Motor Claims Guide — Reporting weather-related damage.";
        }

        if (lower.Contains("windscreen") || lower.Contains("glass"))
        {
            return "Windscreen or vehicle-glass damage may be repairable or replaceable under the glass benefit selected on your policy. " +
                   "Cover and excess differ by policy, so confirm the active schedule before arranging replacement. " +
                   "Source: MiWay Motor Policy Wording — Glass cover.";
        }

        if (lower.Contains("accident") || lower.Contains("collision"))
        {
            return "For an accident claim, record the other party's details, take photographs, report the incident where required, " +
                   "and notify the insurer as soon as reasonably possible. Cover remains subject to your selected benefits, exclusions, excess, and validation. " +
                   "Source: Motor Claims Guide — Accident claims; MiWay Motor Policy Wording — Accidental damage.";
        }

        return "Coverage depends on the active policy schedule, selected benefits, exclusions, excesses, and the facts of the incident. " +
               "A claims consultant should verify those details before confirming cover. " +
               "Source: Customer Service Knowledge — Policy enquiries.";
    }

    private static string ExtractSection(string value, string heading)
    {
        var index = value.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? value : value[(index + heading.Length)..].Trim();
    }

    private static string? ExtractMarker(string value, string name)
    {
        var prefix = $"[{name}:";
        var start = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        start += prefix.Length;
        var end = value.IndexOf(']', start);
        return end < 0 ? null : value[start..end].Trim();
    }
}
