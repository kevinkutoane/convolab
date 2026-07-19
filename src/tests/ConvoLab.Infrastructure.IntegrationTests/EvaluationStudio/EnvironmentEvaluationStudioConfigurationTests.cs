using ConvoLab.Infrastructure.EvaluationStudio;
using Microsoft.Extensions.Configuration;

namespace ConvoLab.Infrastructure.IntegrationTests.EvaluationStudio;

public sealed class EnvironmentEvaluationStudioConfigurationTests
{
    [Fact]
    public void Environment_values_override_appsettings_policy_defaults()
    {
        var keys = new[]
        {
            "CONVOLAB_EVALUATION_MIN_GROUNDEDNESS",
            "CONVOLAB_EVALUATION_MIN_RELEVANCE",
            "CONVOLAB_EVALUATION_MIN_SAFETY",
            "CONVOLAB_EVALUATION_MIN_OVERALL",
            "CONVOLAB_EVALUATION_FAILURE_ACTION"
        };
        var previous = keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(keys[0], "0.75");
            Environment.SetEnvironmentVariable(keys[1], "0.76");
            Environment.SetEnvironmentVariable(keys[2], "0.97");
            Environment.SetEnvironmentVariable(keys[3], "0.84");
            Environment.SetEnvironmentVariable(keys[4], "Block");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Evaluation:MinimumGroundedness"] = "0.80",
                    ["Evaluation:FailureAction"] = "Review"
                })
                .Build();

            var policy = new EnvironmentEvaluationStudioConfiguration(configuration).GetPolicy();

            Assert.Equal(.75, policy.MinimumGroundedness);
            Assert.Equal(.76, policy.MinimumRelevance);
            Assert.Equal(.97, policy.MinimumSafety);
            Assert.Equal(.84, policy.MinimumOverallScore);
            Assert.Equal("Block", policy.FailureAction);
        }
        finally
        {
            foreach (var item in previous)
                Environment.SetEnvironmentVariable(item.Key, item.Value);
        }
    }
}
