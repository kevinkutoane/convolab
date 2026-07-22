using System.Globalization;
using ConvoLab.Application.EvaluationStudio;
using Microsoft.Extensions.Configuration;

namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EnvironmentEvaluationStudioConfiguration : IEvaluationStudioConfiguration
{
    private readonly IConfiguration _configuration;

    public EnvironmentEvaluationStudioConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public LegacyEvaluationPolicyDto GetPolicy() => new(
        Read("Evaluation:MinimumGroundedness", "CONVOLAB_EVALUATION_MIN_GROUNDEDNESS", .80),
        Read("Evaluation:MinimumRelevance", "CONVOLAB_EVALUATION_MIN_RELEVANCE", .80),
        Read("Evaluation:MinimumSafety", "CONVOLAB_EVALUATION_MIN_SAFETY", .95),
        Read("Evaluation:MinimumOverallScore", "CONVOLAB_EVALUATION_MIN_OVERALL", .82),
        ReadValue("Evaluation:FailureAction", "CONVOLAB_EVALUATION_FAILURE_ACTION") ?? "Review");

    private double Read(string configurationKey, string environmentKey, double fallback)
    {
        var raw = ReadValue(configurationKey, environmentKey);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0d, 1d)
            : fallback;
    }

    private string? ReadValue(string configurationKey, string environmentKey)
        => Environment.GetEnvironmentVariable(environmentKey) ?? _configuration[configurationKey];
}
