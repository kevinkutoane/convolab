using System.Globalization;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntelligenceStudio;

public sealed class PersistedIntelligenceStudioConfiguration(
    ApplicationDbContext db,
    IEffectiveConfigurationResolver effective,
    IRuntimeRequestContext runtime)
    : IIntelligenceStudioConfiguration
{
    private IReadOnlyList<EffectiveSettingResult>? cached;

    public decimal MonthlyBudgetZar =>
        Decimal(SettingKeys.MonthlyBudgetZar, 500m) ?? 500m;

    public IReadOnlyList<IntelligenceProviderDefinition> GetProviders()
    {
        var model = Text(SettingKeys.AiModel, "gemini-2.5-flash");
        var secretReference = Text(SettingKeys.AiSecretReference, "");
        var providerEnabled = Boolean(SettingKeys.AiProviderEnabled, true);
        var geminiConfigured = providerEnabled
            && !string.IsNullOrWhiteSpace(secretReference)
            && db.SecretReferences.AsNoTracking().Any(item =>
                item.WorkspaceId == runtime.WorkspaceId
                && item.Reference == secretReference
                && !item.IsDisabled
                && item.Status != "Missing");

        return
        [
            new IntelligenceProviderDefinition(
                "Deterministic",
                "ConvoLab Deterministic (Local test provider)",
                providerEnabled,
                false,
                providerEnabled ? "Ready" : "Disabled",
                providerEnabled
                    ? "Repeatable rule-based responses with synthetic usage and estimated cost; no external model or key is required."
                    : "Provider execution is disabled by the effective environment settings.",
                [
                    new IntelligenceModelDefinition(
                        "convolab-deterministic-primary",
                        "Deterministic Primary",
                        ["Chat", "TextGeneration", "Streaming", "StructuredOutput"],
                        32_000,
                        4_000,
                        140,
                        .02m,
                        .04m,
                        "ZAR"),
                    new IntelligenceModelDefinition(
                        "convolab-deterministic-fallback",
                        "Deterministic Fallback",
                        ["Chat", "TextGeneration", "Streaming", "StructuredOutput"],
                        32_000,
                        4_000,
                        220,
                        .04m,
                        .06m,
                        "ZAR")
                ]),
            new IntelligenceProviderDefinition(
                "Gemini",
                "Google Gemini",
                geminiConfigured,
                true,
                geminiConfigured ? "Ready" : "Not configured",
                geminiConfigured
                    ? null
                    : "Configure ai.secret_reference with a validated secret reference in Runtime Settings.",
                [
                    new IntelligenceModelDefinition(
                        model,
                        model,
                        ["Chat", "TextGeneration", "Streaming", "StructuredOutput", "Vision", "Reasoning"],
                        1_000_000,
                        Integer(SettingKeys.AiMaxOutputTokens, 8_192),
                        900,
                        Decimal(SettingKeys.AiInputPriceZarPer1K),
                        Decimal(SettingKeys.AiOutputPriceZarPer1K),
                        "ZAR")
                ])
        ];
    }

    private IReadOnlyList<EffectiveSettingResult> Settings()
    {
        if (cached is not null) return cached;
        var organisationId = runtime.OrganisationId;
        var workspaceId = runtime.WorkspaceId;
        var environmentId = runtime.EnvironmentId;
        if (!organisationId.HasValue || !workspaceId.HasValue || !environmentId.HasValue)
            return cached = [];
        return cached = effective.ResolveAsync(
                organisationId.Value,
                workspaceId.Value,
                environmentId.Value)
            .GetAwaiter()
            .GetResult();
    }

    private string Text(string key, string fallback)
    {
        var raw = Settings().FirstOrDefault(item => item.Key == key)?.EffectiveValue;
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return raw.Trim().Trim('"');
    }

    private bool Boolean(string key, bool fallback) =>
        bool.TryParse(Text(key, fallback.ToString()), out var value)
            ? value
            : fallback;

    private int Integer(string key, int fallback) =>
        int.TryParse(
            Text(key, fallback.ToString(CultureInfo.InvariantCulture)),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, 1, 65_536)
            : fallback;

    private decimal? Decimal(string key, decimal? fallback = null) =>
        decimal.TryParse(
            Text(key, fallback?.ToString(CultureInfo.InvariantCulture) ?? ""),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
}
