using ConvoLab.Infrastructure.IntelligenceStudio;
using Microsoft.Extensions.Configuration;

namespace ConvoLab.Infrastructure.IntegrationTests.IntelligenceStudio;

public sealed class EnvironmentIntelligenceStudioConfigurationTests
{
    [Fact]
    public void Custom_environment_values_override_appsettings_defaults()
    {
        var keys = new[]
        {
            "CONVOLAB_MONTHLY_AI_BUDGET_ZAR",
            "GEMINI_API_KEY",
            "GEMINI_MODEL",
            "GEMINI_INPUT_PRICE_ZAR_PER_1K",
            "GEMINI_OUTPUT_PRICE_ZAR_PER_1K"
        };
        var previousValues = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable("CONVOLAB_MONTHLY_AI_BUDGET_ZAR", "750.50");
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("GEMINI_MODEL", "gemini-test-model");
            Environment.SetEnvironmentVariable("GEMINI_INPUT_PRICE_ZAR_PER_1K", "2.25");
            Environment.SetEnvironmentVariable("GEMINI_OUTPUT_PRICE_ZAR_PER_1K", "7.50");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Intelligence:MonthlyBudgetZar"] = "500",
                    ["Gemini:Model"] = "appsettings-model"
                })
                .Build();
            var sut = new EnvironmentIntelligenceStudioConfiguration(configuration);

            Assert.Equal(750.50m, sut.MonthlyBudgetZar);
            var gemini = Assert.Single(sut.GetProviders(), provider => provider.Key == "Gemini");
            Assert.True(gemini.IsConfigured);
            var model = Assert.Single(gemini.Models);
            Assert.Equal("gemini-test-model", model.Key);
            Assert.Equal(2.25m, model.InputPricePer1K);
            Assert.Equal(7.50m, model.OutputPricePer1K);
            Assert.Equal("ZAR", model.Currency);
        }
        finally
        {
            foreach (var pair in previousValues)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
