using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Services;
using ConvoLab.Domain.Intelligence.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Intelligence;

public class ExecutionPlannerTests
{
    private readonly ExecutionPlanner _planner = new();
    private readonly IntelligenceProvider _provider;

    public ExecutionPlannerTests()
    {
        _provider = IntelligenceProvider.Register("OpenAI", ProviderKind.OpenAI);
        _provider.AddModel(
            "gpt-4",
            CapabilitySet.Of(IntelligenceCapability.Chat, IntelligenceCapability.Streaming, IntelligenceCapability.ToolCalling),
            ModelPricing.Create(0.03m, 0.06m),
            maxContextTokens: 8192,
            maxOutputTokens: 4096,
            typicalLatency: TimeSpan.FromSeconds(2));

        _provider.AddModel(
            "gpt-3.5-turbo",
            CapabilitySet.Of(IntelligenceCapability.Chat, IntelligenceCapability.Streaming),
            ModelPricing.Create(0.0015m, 0.002m),
            maxContextTokens: 4096,
            maxOutputTokens: 2048,
            typicalLatency: TimeSpan.FromSeconds(1));

        _provider.ReportHealth(ProviderHealthSnapshot.Create(ProviderAvailability.Available, TimeSpan.FromSeconds(1), 0.0));
    }

    [Fact]
    public void CreatePlan_ShouldSelectModelMeetingCapabilitiesAndLimits()
    {
        // Arrange
        var context = ConvoLab.Domain.Intelligence.ValueObjects.ExecutionContext.Create(estimatedPromptTokens: 1000);
        var requirement = ExecutionRequirement.Create(
            capabilities: CapabilitySet.Of(IntelligenceCapability.Chat, IntelligenceCapability.ToolCalling),
            requiresTools: true,
            maxOutputTokens: 2000);
        var policy = ExecutionPolicy.Create(maxCostPerExecution: ExecutionCost.Create(1.00m));

        // Act
        var plan = _planner.CreatePlan(context, requirement, policy, new[] { _provider });

        // Assert
        Assert.Equal("gpt-4", plan.ModelName); // gpt-3.5 lacks ToolCalling
        Assert.True(plan.AllowTools);
        Assert.Equal(0.15m, plan.EstimatedCost.Amount); // (1000 * 0.03/1k) + (2000 * 0.06/1k) = 0.03 + 0.12 = 0.15
    }

    [Fact]
    public void CreatePlan_ShouldThrowWhenNoModelCanServeRequirement()
    {
        // Arrange
        var context = ConvoLab.Domain.Intelligence.ValueObjects.ExecutionContext.Create(estimatedPromptTokens: 5000);
        var requirement = ExecutionRequirement.Create(
            capabilities: CapabilitySet.Of(IntelligenceCapability.Chat),
            maxOutputTokens: 4000); // 5k + 4k = 9k > 8192 context limit
        var policy = ExecutionPolicy.Default();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _planner.CreatePlan(context, requirement, policy, new[] { _provider }));
    }

    [Fact]
    public void CreatePlan_ShouldThrowWhenEstimatedCostExceedsPolicy()
    {
        // Arrange
        var context = ConvoLab.Domain.Intelligence.ValueObjects.ExecutionContext.Create(estimatedPromptTokens: 1000);
        var requirement = ExecutionRequirement.Create(
            capabilities: CapabilitySet.Of(IntelligenceCapability.Chat, IntelligenceCapability.ToolCalling),
            maxOutputTokens: 2000);
        var policy = ExecutionPolicy.Create(maxCostPerExecution: ExecutionCost.Create(0.10m)); // Plan needs 0.15

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _planner.CreatePlan(context, requirement, policy, new[] { _provider }));
    }

    [Fact]
    public void CreateFallbackPlan_ShouldReturnPlanForSpecificModel()
    {
        // Arrange
        var context = ConvoLab.Domain.Intelligence.ValueObjects.ExecutionContext.Create(estimatedPromptTokens: 500);
        var requirement = ExecutionRequirement.Create(capabilities: CapabilitySet.Of(IntelligenceCapability.Chat));
        var policy = ExecutionPolicy.Default();

        var primaryPlan = _planner.CreatePlan(context, requirement, policy, new[] { _provider });
        Assert.Equal("gpt-3.5-turbo", primaryPlan.ModelName); // Cheaper model wins
        Assert.True(primaryPlan.FallbackPolicy.HasFallback);

        var fallbackModelId = primaryPlan.FallbackPolicy.FallbackModels.First();

        // Act
        var fallbackPlan = _planner.CreateFallbackPlan(context, requirement, policy, new[] { _provider }, fallbackModelId);

        // Assert
        Assert.Equal("gpt-4", fallbackPlan.ModelName);
        Assert.False(fallbackPlan.FallbackPolicy.HasFallback); // Fallback plans don't have further fallbacks
        // ExecutionRetryPolicy.None() creates a policy with MaxAttempts=1, but it still inherits the default RetryableFailures list. We just check MaxAttempts.
        Assert.Equal(1, fallbackPlan.RetryPolicy.MaxAttempts);
    }
}
