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

public sealed class PolicyVersioningTests
{
    [Fact]
    public void Active_policy_is_immutable_and_new_version_is_a_draft()
    {
        var policy = PolicyDefinition.Create("Provider policy", PolicyDomain.ProviderAccess, PolicyEffect.Allow);
        policy.AddRule(PolicyRule.Create("Allow deterministic", PolicyEffect.Allow, 100,
            new Dictionary<string, string> { ["provider"] = "Deterministic" }));
        policy.Activate();

        Assert.Throws<InvalidOperationException>(() => policy.AddRule(
            PolicyRule.Create("Late mutation", PolicyEffect.Deny)));

        var next = policy.CreateNextVersion("Governance Team");
        Assert.Equal(policy.PolicyKey, next.PolicyKey);
        Assert.Equal(2, next.Version);
        Assert.Equal(PolicyStatus.Draft, next.Status);
        Assert.Single(next.Rules);
    }


    [Fact]
    public void Rule_matching_is_case_insensitive_for_attribute_keys_and_values()
    {
        var policy = PolicyDefinition.Create("Case insensitive", PolicyDomain.ProviderAccess, PolicyEffect.Deny);
        policy.AddRule(PolicyRule.Create(
            "Allow deterministic",
            PolicyEffect.Allow,
            100,
            new Dictionary<string, string> { ["provider"] = "deterministic" }));
        policy.Activate();

        var decision = policy.Evaluate(PolicyEvaluationContext.Create(
            PolicyDomain.ProviderAccess,
            attributes: new Dictionary<string, string> { ["Provider"] = "Deterministic" }));

        Assert.Equal(PolicyEffect.Allow, decision.Effect);
    }

    [Fact]
    public void Environment_policy_abstains_outside_its_environment()
    {
        var policy = PolicyDefinition.Create(
            "Production only",
            PolicyDomain.ModelAccess,
            PolicyEffect.Deny,
            scope: PolicyScope.Environment,
            environment: "Production");
        policy.AddRule(PolicyRule.Create("Allow approved", PolicyEffect.Allow, 100,
            new Dictionary<string, string> { ["model"] = "approved" }));
        policy.Activate();

        var decision = policy.Evaluate(PolicyEvaluationContext.Create(
            PolicyDomain.ModelAccess,
            attributes: new Dictionary<string, string>
            {
                ["environment"] = "Development",
                ["model"] = "unapproved"
            }));

        Assert.Equal(PolicyEffect.Allow, decision.Effect);
        Assert.Contains("does not apply", decision.Reason);
    }
}
