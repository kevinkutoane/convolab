using ConvoLab.Domain.Analytics;

namespace ConvoLab.Domain.Tests.Analytics;

public sealed class AnalyticsTests
{
    [Fact]
    public void Configuration_revision_depends_on_values_not_input_order()
    {
        var first = AnalyticsKeys.ConfigurationRevision(new Dictionary<string, string?>
        {
            ["model"] = "gemini-2.5-flash",
            ["provider"] = "Gemini"
        });
        var second = AnalyticsKeys.ConfigurationRevision(new Dictionary<string, string?>
        {
            ["provider"] = "Gemini",
            ["model"] = "gemini-2.5-flash"
        });

        Assert.Equal(first, second);
        Assert.StartsWith("sha256:", first);
    }

    [Fact]
    public void Cost_is_unavailable_when_pricing_is_missing_and_never_silently_zero()
    {
        var cost = AnalyticsCost.Classify(null, 100, 50, null, null, null);

        Assert.Equal(AnalyticsCostType.Unavailable, cost.Type);
        Assert.Null(cost.AmountZar);
    }

    [Fact]
    public void Estimated_cost_uses_decimal_per_thousand_formula()
    {
        var cost = AnalyticsCost.Classify(null, 1_500, 250, 0.02m, 0.06m, "pricing:v1");

        Assert.Equal(AnalyticsCostType.Estimated, cost.Type);
        Assert.Equal(0.045m, cost.AmountZar);
        Assert.Equal("pricing:v1", cost.PricingRevision);
    }

    [Fact]
    public void Policy_prevention_records_explicit_zero_actual_cost()
    {
        var cost = AnalyticsCost.Classify(null, null, null, null, null, null, providerInvocationPrevented: true);

        Assert.Equal(AnalyticsCostType.Actual, cost.Type);
        Assert.Equal(0m, cost.AmountZar);
        Assert.Equal("policy-prevented", cost.PricingRevision);
    }

    [Fact]
    public void Event_keys_are_deterministic()
    {
        var sourceId = Guid.Parse("f4d5b13a-cd7c-457e-ae40-8470e89cded6");

        Assert.Equal(
            AnalyticsKeys.Event("SimulationRun", sourceId, "SimulationExecution"),
            AnalyticsKeys.Event("simulationrun", sourceId, "simulationexecution"));
    }
}
