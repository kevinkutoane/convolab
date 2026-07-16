using ConvoLab.Domain.Policy.Aggregates;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Policy;

public class PolicyDefinitionTests
{
    [Fact]
    public void Evaluate_ShouldReturnDefaultEffect_WhenNoRulesMatch()
    {
        // Arrange
        var policy = PolicyDefinition.Create("TestPolicy", PolicyDomain.ModelAccess, PolicyEffect.Deny);
        policy.Activate();

        var context = PolicyEvaluationContext.Create(PolicyDomain.ModelAccess);

        // Act
        var decision = policy.Evaluate(context);

        // Assert
        Assert.Equal(PolicyEffect.Deny, decision.Effect);
        Assert.Contains("No rule matched", decision.Reason);
    }

    [Fact]
    public void Evaluate_ShouldApplyHighestPriorityMatchingRule()
    {
        // Arrange
        var policy = PolicyDefinition.Create("TestPolicy", PolicyDomain.ModelAccess, PolicyEffect.Deny);

        policy.AddRule(PolicyRule.Create(
            "LowPriorityAllow",
            PolicyEffect.Allow,
            priority: 10,
            match: new Dictionary<string, string> { { "role", "user" } }));

        policy.AddRule(PolicyRule.Create(
            "HighPriorityDeny",
            PolicyEffect.Deny,
            priority: 100,
            match: new Dictionary<string, string> { { "role", "user" }, { "department", "external" } }));

        policy.Activate();

        var contextLow = PolicyEvaluationContext.Create(PolicyDomain.ModelAccess, null, new Dictionary<string, string> { { "role", "user" } });
        var contextHigh = PolicyEvaluationContext.Create(PolicyDomain.ModelAccess, null, new Dictionary<string, string> { { "role", "user" }, { "department", "external" } });

        // Act
        var decisionLow = policy.Evaluate(contextLow);
        var decisionHigh = policy.Evaluate(contextHigh);

        // Assert
        Assert.Equal(PolicyEffect.Allow, decisionLow.Effect);
        Assert.Contains("LowPriorityAllow", decisionLow.Reason);

        Assert.Equal(PolicyEffect.Deny, decisionHigh.Effect);
        Assert.Contains("HighPriorityDeny", decisionHigh.Reason);
    }

    [Fact]
    public void Evaluate_ShouldAbstain_WhenInactiveOrWrongDomain()
    {
        // Arrange
        var policy = PolicyDefinition.Create("TestPolicy", PolicyDomain.ModelAccess, PolicyEffect.Deny);

        // Act & Assert (Draft)
        var decisionDraft = policy.Evaluate(PolicyEvaluationContext.Create(PolicyDomain.ModelAccess));
        Assert.Equal(PolicyEffect.Allow, decisionDraft.Effect); // Abstain
        Assert.Contains("not active", decisionDraft.Reason);

        policy.Activate();

        // Act & Assert (Wrong Domain)
        var decisionDomain = policy.Evaluate(PolicyEvaluationContext.Create(PolicyDomain.BudgetLimit));
        Assert.Equal(PolicyEffect.Allow, decisionDomain.Effect); // Abstain
        Assert.Contains("does not govern", decisionDomain.Reason);
    }
}
