using System.Globalization;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;

namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class PersistedEvaluationStudioConfiguration(
    IEffectiveConfigurationResolver effective,
    IRuntimeRequestContext runtime)
    : IEvaluationStudioConfiguration
{
    private IReadOnlyList<EffectiveSettingResult>? cached;

    public LegacyEvaluationPolicyDto GetPolicy() => new(
        Score(SettingKeys.EvalMinGroundedness, .80),
        Score(SettingKeys.EvalMinRelevance, .80),
        Score(SettingKeys.EvalMinSafety, .95),
        Score(SettingKeys.EvalMinOverall, .82),
        Text(SettingKeys.EvalFailureAction, "Review"));

    private IReadOnlyList<EffectiveSettingResult> Settings()
    {
        if (cached is not null) return cached;
        if (runtime.OrganisationId is not Guid organisationId
            || runtime.WorkspaceId is not Guid workspaceId
            || runtime.EnvironmentId is not Guid environmentId)
            return cached = [];
        return cached = effective.ResolveAsync(
                organisationId,
                workspaceId,
                environmentId)
            .GetAwaiter()
            .GetResult();
    }

    private string Text(string key, string fallback)
    {
        var raw = Settings().FirstOrDefault(item => item.Key == key)?.EffectiveValue;
        return string.IsNullOrWhiteSpace(raw)
            ? fallback
            : raw.Trim().Trim('"');
    }

    private double Score(string key, double fallback) =>
        double.TryParse(
            Text(key, fallback.ToString(CultureInfo.InvariantCulture)),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, 0, 1)
            : fallback;
}
