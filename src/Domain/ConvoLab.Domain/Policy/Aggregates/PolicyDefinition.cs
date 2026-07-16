using ConvoLab.Domain.Common;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.Events;
using ConvoLab.Domain.Policy.ValueObjects;

namespace ConvoLab.Domain.Policy.Aggregates;

/// <summary>
/// A governance policy: an ordered set of rules over a policy domain
/// (model access, budget limits, rate limits, prompt approval, knowledge
/// access, tenant rules, compliance, evaluation thresholds...).
///
/// Execution engines never decide whether something is allowed — they ask.
/// This separation lets business rules evolve independently of execution.
/// </summary>
public class PolicyDefinition : BaseAggregateRoot<PolicyDefinitionId>
{
    private readonly List<PolicyRule> _rules = new();

    public string Name { get; private set; }
    public PolicyDomain Domain { get; private set; }
    public PolicyStatus Status { get; private set; }
    public Guid? TenantId { get; private set; }

    /// <summary>The effect applied when no rule matches. Deny-by-default is the safe posture.</summary>
    public PolicyEffect DefaultEffect { get; private set; }

    public IReadOnlyList<PolicyRule> Rules => _rules.AsReadOnly();

    private PolicyDefinition() : base() { Name = null!; } // For EF Core

    private PolicyDefinition(PolicyDefinitionId id, string name, PolicyDomain domain, PolicyEffect defaultEffect, Guid? tenantId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Policy name is required.");
        Name = name;
        Domain = domain;
        DefaultEffect = defaultEffect;
        TenantId = tenantId;
        Status = PolicyStatus.Draft;
    }

    public static PolicyDefinition Create(string name, PolicyDomain domain, PolicyEffect defaultEffect = PolicyEffect.Deny, Guid? tenantId = null)
    {
        var policy = new PolicyDefinition(PolicyDefinitionId.CreateUnique(), name, domain, defaultEffect, tenantId);
        policy.AddDomainEvent(new PolicyCreatedEvent(policy.Id, name, domain));
        return policy;
    }

    public void AddRule(PolicyRule rule)
    {
        if (Status == PolicyStatus.Retired)
            throw new InvalidOperationException("Cannot modify a retired policy.");
        if (_rules.Any(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Rule '{rule.Name}' already exists in policy '{Name}'.");
        _rules.Add(rule);
    }

    public void Activate()
    {
        if (Status == PolicyStatus.Retired)
            throw new InvalidOperationException("A retired policy cannot be activated.");
        Status = PolicyStatus.Active;
        AddDomainEvent(new PolicyActivatedEvent(Id, Name));
    }

    public void Suspend() => Status = PolicyStatus.Suspended;
    public void Retire() => Status = PolicyStatus.Retired;

    /// <summary>
    /// Evaluates the context: highest-priority matching rule wins; the
    /// default effect applies when nothing matches. Inactive policies allow —
    /// they simply abstain.
    /// </summary>
    public PolicyDecision Evaluate(PolicyEvaluationContext context)
    {
        if (Status != PolicyStatus.Active)
            return PolicyDecision.Allow($"Policy '{Name}' is not active; abstained.", Id);

        if (context.Domain != Domain)
            return PolicyDecision.Allow($"Policy '{Name}' does not govern domain '{context.Domain}'; abstained.", Id);

        var winning = _rules
            .Where(r => r.Matches(context))
            .OrderByDescending(r => r.Priority)
            .FirstOrDefault();

        var decision = winning is null
            ? DecisionFor(DefaultEffect, $"No rule matched; default effect of policy '{Name}' applied.", null)
            : DecisionFor(winning.Effect, $"Rule '{winning.Name}' of policy '{Name}' applied.", winning);

        AddDomainEvent(new PolicyEvaluatedEvent(Id, context.Domain, decision.Effect));
        return decision;
    }

    private PolicyDecision DecisionFor(PolicyEffect effect, string reason, PolicyRule? rule) => effect switch
    {
        PolicyEffect.Allow => PolicyDecision.Allow(reason, Id),
        PolicyEffect.AllowWithConstraints => PolicyDecision.AllowWithConstraints(
            rule is null
                ? new Dictionary<string, string>()
                : rule.Constraints.ToDictionary(kv => kv.Key, kv => kv.Value),
            reason, Id),
        _ => PolicyDecision.Deny(reason, Id)
    };
}
