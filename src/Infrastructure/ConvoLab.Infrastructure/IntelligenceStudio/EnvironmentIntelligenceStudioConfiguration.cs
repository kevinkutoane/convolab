using ConvoLab.Application.IntelligenceStudio;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace ConvoLab.Infrastructure.IntelligenceStudio;

public sealed class EnvironmentIntelligenceStudioConfiguration : IIntelligenceStudioConfiguration
{
    private readonly IConfiguration _configuration;

    public EnvironmentIntelligenceStudioConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public decimal MonthlyBudgetZar
    {
        get
        {
            var raw = ReadValue("Intelligence:MonthlyBudgetZar", "CONVOLAB_MONTHLY_AI_BUDGET_ZAR");
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0m
                ? value
                : 500m;
        }
    }

    public IReadOnlyList<IntelligenceProviderDefinition> GetProviders()
    {
        var geminiKey = ReadValue("Gemini:ApiKey", "GEMINI_API_KEY");
        var geminiModel = ReadValue("Gemini:Model", "GEMINI_MODEL") ?? "gemini-2.5-flash";
        var geminiInput = ReadNullableDecimal("Gemini:InputPriceZarPer1K", "GEMINI_INPUT_PRICE_ZAR_PER_1K");
        var geminiOutput = ReadNullableDecimal("Gemini:OutputPriceZarPer1K", "GEMINI_OUTPUT_PRICE_ZAR_PER_1K");
        var geminiConfigured = !string.IsNullOrWhiteSpace(geminiKey);

        return new[]
        {
            new IntelligenceProviderDefinition(
                "Deterministic",
                "ConvoLab Deterministic",
                true,
                false,
                "Ready",
                null,
                new[]
                {
                    new IntelligenceModelDefinition(
                        "convolab-deterministic-primary",
                        "Deterministic Primary",
                        new[] { "Chat", "TextGeneration", "Streaming", "StructuredOutput" },
                        32_000,
                        4_000,
                        140,
                        0.02m,
                        0.04m,
                        "ZAR"),
                    new IntelligenceModelDefinition(
                        "convolab-deterministic-fallback",
                        "Deterministic Fallback",
                        new[] { "Chat", "TextGeneration", "Streaming", "StructuredOutput" },
                        32_000,
                        4_000,
                        220,
                        0.04m,
                        0.06m,
                        "ZAR")
                }),
            new IntelligenceProviderDefinition(
                "Gemini",
                "Google Gemini",
                geminiConfigured,
                true,
                geminiConfigured ? "Ready" : "Not configured",
                geminiConfigured ? null : "Set GEMINI_API_KEY on the API host.",
                new[]
                {
                    new IntelligenceModelDefinition(
                        geminiModel,
                        geminiModel,
                        new[] { "Chat", "TextGeneration", "Streaming", "StructuredOutput", "Vision", "Reasoning" },
                        ReadInt("Gemini:MaxContextTokens", "GEMINI_MAX_CONTEXT_TOKENS", 1_000_000),
                        ReadInt("Gemini:MaxOutputTokens", "GEMINI_MAX_OUTPUT_TOKENS", 8_192),
                        ReadDouble("Gemini:TypicalLatencyMs", "GEMINI_TYPICAL_LATENCY_MS", 900),
                        geminiInput,
                        geminiOutput,
                        "ZAR")
                })
        };
    }

    private decimal? ReadNullableDecimal(string configKey, string environmentKey)
    {
        var raw = ReadValue(configKey, environmentKey);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0m
            ? value
            : null;
    }

    private int ReadInt(string configKey, string environmentKey, int fallback)
    {
        var raw = ReadValue(configKey, environmentKey);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }

    private double ReadDouble(string configKey, string environmentKey, double fallback)
    {
        var raw = ReadValue(configKey, environmentKey);
        return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }

    private string? ReadValue(string configKey, string environmentKey)
    {
        var environmentValue = Environment.GetEnvironmentVariable(environmentKey);
        return !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue
            : _configuration[configKey];
    }
}
