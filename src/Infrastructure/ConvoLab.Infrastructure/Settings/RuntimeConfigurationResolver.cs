using System.Globalization;
using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;

namespace ConvoLab.Infrastructure.Settings;

public sealed class RuntimeConfigurationResolver(IEffectiveConfigurationResolver effective)
    : IRuntimeConfigurationResolver
{
    public async Task<RuntimeExecutionConfiguration> ResolveAsync(
        IRuntimeRequestContext context,
        RuntimeExecutionOverrides? overrides = null,
        CancellationToken cancellationToken = default)
    {
        if (context.OrganisationId is not Guid organisationId
            || context.WorkspaceId is not Guid workspaceId
            || context.EnvironmentId is not Guid environmentId)
            throw new ResourceConflictException(
                "runtime_environment.default_unavailable",
                "A trusted organisation, workspace, and runtime environment are required.");

        ValidateOverrides(overrides);
        var overrideValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(overrides?.Provider))
            overrideValues["provider"] = overrides.Provider.Trim();
        if (!string.IsNullOrWhiteSpace(overrides?.Model))
            overrideValues["model"] = overrides.Model.Trim();
        if (overrides?.Temperature.HasValue == true)
            overrideValues["temperature"] = overrides.Temperature.Value
                .ToString(CultureInfo.InvariantCulture);
        if (overrides?.MaximumOutputTokens.HasValue == true)
            overrideValues["maximumOutputTokens"] = overrides.MaximumOutputTokens.Value
                .ToString(CultureInfo.InvariantCulture);

        var settings = await effective.ResolveAsync(
            organisationId, workspaceId, environmentId, cancellationToken);
        var snapshot = await effective.CreateSnapshotAsync(
            organisationId,
            workspaceId,
            environmentId,
            cancellationToken,
            overrideValues);

        string? Value(string key) => settings.FirstOrDefault(item => item.Key == key)?.EffectiveValue;
        string Text(string key, string fallback = "") => Scalar(Value(key)) ?? fallback;
        bool Boolean(string key, bool fallback) =>
            bool.TryParse(Text(key), out var value) ? value : fallback;
        int Integer(string key, int fallback, int minimum, int maximum) =>
            int.TryParse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, minimum, maximum)
                : fallback;
        decimal Decimal(string key, decimal fallback, decimal minimum, decimal maximum) =>
            decimal.TryParse(Text(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, minimum, maximum)
                : fallback;
        decimal? NullableDecimal(string key)
            => decimal.TryParse(Text(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                && value >= 0 ? value : null;
        double Score(string key, double fallback)
            => double.TryParse(Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, 0d, 1d)
                : fallback;

        var featureKeys = new[]
        {
            SettingKeys.FeatureProviderExecution,
            SettingKeys.FeatureReplayExecution,
            SettingKeys.FeaturePluginActivation,
            SettingKeys.FeaturePolicyEnforcement,
            SettingKeys.FeatureExperimental,
            SettingKeys.FeatureSensitiveTraceReveal
        };
        var pluginKeys = new[]
        {
            SettingKeys.PluginAllowWorkspaceRegistration,
            SettingKeys.PluginAllowManifestUrl,
            SettingKeys.PluginRequireHealthy,
            SettingKeys.PluginRequireCompatibility,
            SettingKeys.PluginAllowPlatform
        };
        var provider = overrides?.Provider?.Trim()
            ?? Text(SettingKeys.AiProvider, "Deterministic");
        var model = overrides?.Model?.Trim()
            ?? Text(SettingKeys.AiModel, "convolab-deterministic-primary");
        var inputPrice = NullableDecimal(SettingKeys.AiInputPriceZarPer1K);
        var outputPrice = NullableDecimal(SettingKeys.AiOutputPriceZarPer1K);
        if (provider.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
        {
            inputPrice ??= model.EndsWith("fallback", StringComparison.OrdinalIgnoreCase)
                ? .04m
                : .02m;
            outputPrice ??= model.EndsWith("fallback", StringComparison.OrdinalIgnoreCase)
                ? .06m
                : .04m;
        }

        return new RuntimeExecutionConfiguration(
            snapshot,
            provider,
            model,
            NullIfEmpty(Text(SettingKeys.AiSecretReference)),
            overrides?.Temperature ?? (double)Decimal(SettingKeys.AiTemperature, .7m, 0m, 2m),
            overrides?.MaximumOutputTokens
                ?? Integer(SettingKeys.AiMaxOutputTokens, 8192, 1, 65536),
            Integer(SettingKeys.AiRequestTimeoutSeconds, 30, 5, 300),
            Integer(SettingKeys.AiMaxRetryCount, 3, 0, 10),
            Decimal(SettingKeys.MonthlyBudgetZar, 500m, 0m, decimal.MaxValue),
            Decimal(SettingKeys.BudgetWarningThreshold, .8m, 0m, 1m),
            Decimal(SettingKeys.BudgetHardStopThreshold, 1m, 0m, 1m),
            inputPrice,
            outputPrice,
            Boolean(SettingKeys.AllowExecutionWhenPricingUnknown, true),
            Score(SettingKeys.EvalMinGroundedness, .8),
            Score(SettingKeys.EvalMinRelevance, .8),
            Score(SettingKeys.EvalMinSafety, .95),
            Score(SettingKeys.EvalMinOverall, .82),
            Text(SettingKeys.EvalFailureAction, "Review"),
            Boolean(SettingKeys.PolicyEnforcementEnabled, true)
                && Boolean(SettingKeys.FeaturePolicyEnforcement, true),
            Text(SettingKeys.PolicyDefaultDenialBehaviour, "Allow"),
            Boolean(SettingKeys.AiProviderEnabled, true)
                && Boolean(SettingKeys.FeatureProviderExecution, true),
            Boolean(SettingKeys.FeatureReplayExecution, true),
            Boolean(SettingKeys.FeaturePluginActivation, true),
            Boolean(SettingKeys.AllowSensitiveArtifactReveal, false)
                && Boolean(SettingKeys.FeatureSensitiveTraceReveal, false),
            Text(SettingKeys.DefaultRedactionLevel, "Standard"),
            pluginKeys.ToDictionary(key => key, key => Value(key), StringComparer.Ordinal),
            featureKeys.ToDictionary(key => key, key => Value(key), StringComparer.Ordinal));
    }

    private static void ValidateOverrides(RuntimeExecutionOverrides? overrides)
    {
        if (overrides is null) return;
        if (overrides.Provider is { Length: > 120 }
            || overrides.Provider is not null && string.IsNullOrWhiteSpace(overrides.Provider))
            throw new RequestValidationException(
                "runtime_configuration.provider_override_invalid",
                "The provider override is invalid.");
        if (overrides.Model is { Length: > 200 }
            || overrides.Model is not null && string.IsNullOrWhiteSpace(overrides.Model))
            throw new RequestValidationException(
                "runtime_configuration.model_override_invalid",
                "The model override is invalid.");
        if (overrides.Temperature is < 0 or > 2)
            throw new RequestValidationException(
                "runtime_configuration.temperature_override_invalid",
                "Temperature must be between 0 and 2.");
        if (overrides.MaximumOutputTokens is < 1 or > 65_536)
            throw new RequestValidationException(
                "runtime_configuration.output_tokens_override_invalid",
                "Maximum output tokens must be between 1 and 65,536.");
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Scalar(string? value)
    {
        if (value is null) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }
}
