using ConvoLab.Domain.Common;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.Events;
using ConvoLab.Domain.Policy.ValueObjects;

namespace ConvoLab.Domain.Policy.Aggregates;

/// <summary>
/// A versioned governance policy: an ordered set of rules over a policy domain.
/// Engines ask Policy for a decision and obey the result; they do not duplicate
/// governance rules inside execution code.
/// </summary>
public class PolicyDefinition : BaseAggregateRoot<PolicyDefinitionId>
{
    private readonly List<PolicyRule> _rules = new();

    public Guid PolicyKey { get; private set; }
    public int Version { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Owner { get; private set; }
    public PolicyDomain Domain { get; private set; }
    public PolicyStatus Status { get; private set; }
    public PolicyScope Scope { get; private set; }
    public string Environment { get; private set; }
    public Guid? TenantId { get; private set; }
    public PolicyEffect DefaultEffect { get; private set; }
    public long Revision { get; private set; }
    public new DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public IReadOnlyList<PolicyRule> Rules => _rules.AsReadOnly();

    private PolicyDefinition() : base()
    {
        Name = null!;
        Description = string.Empty;
        Owner = string.Empty;
        Environment = "All";
    }

    private PolicyDefinition(
        PolicyDefinitionId id,
        Guid policyKey,
        int version,
        string name,
        string description,
        string owner,
        PolicyDomain domain,
        PolicyEffect defaultEffect,
        PolicyScope scope,
        string environment,
        Guid? tenantId,
        PolicyStatus status,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? activatedAt,
        IEnumerable<PolicyRule>? rules = null) : base(id)
    {
        ValidateMetadata(name, owner, version, scope, tenantId);
        PolicyKey = policyKey == Guid.Empty ? Guid.NewGuid() : policyKey;
        Version = version;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Owner = owner.Trim();
        Domain = domain;
        DefaultEffect = defaultEffect;
        Scope = scope;
        Environment = string.IsNullOrWhiteSpace(environment) ? "All" : environment.Trim();
        TenantId = tenantId;
        Status = status;
        Revision = revision;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ActivatedAt = activatedAt;
        if (rules is not null) _rules.AddRange(rules);
    }

    public static PolicyDefinition Create(
        string name,
        PolicyDomain domain,
        PolicyEffect defaultEffect = PolicyEffect.Deny,
        Guid? tenantId = null,
        string description = "",
        string owner = "Platform Engineering",
        PolicyScope scope = PolicyScope.Global,
        string environment = "All")
    {
        var now = DateTimeOffset.UtcNow;
        var effectiveScope = tenantId.HasValue && scope == PolicyScope.Global ? PolicyScope.Tenant : scope;
        var policy = new PolicyDefinition(
            PolicyDefinitionId.CreateUnique(), Guid.NewGuid(), 1, name, description, owner,
            domain, defaultEffect, effectiveScope, environment, tenantId,
            PolicyStatus.Draft, 1, now, now, null);
        policy.AddDomainEvent(new PolicyCreatedEvent(policy.Id, name, domain));
        return policy;
    }

    public static PolicyDefinition Restore(
        PolicyDefinitionId id,
        Guid policyKey,
        int version,
        string name,
        string description,
        string owner,
        PolicyDomain domain,
        PolicyEffect defaultEffect,
        PolicyScope scope,
        string environment,
        Guid? tenantId,
        PolicyStatus status,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? activatedAt,
        IEnumerable<PolicyRule> rules)
        => new(id, policyKey, version, name, description, owner, domain, defaultEffect, scope,
            environment, tenantId, status, revision, createdAt, updatedAt, activatedAt, rules);

    public PolicyDefinition CreateNextVersion(string owner)
    {
        if (Status is not (PolicyStatus.Active or PolicyStatus.Suspended))
            throw new InvalidOperationException("A new version can only be created from an active or suspended policy.");

        var now = DateTimeOffset.UtcNow;
        return new PolicyDefinition(
            PolicyDefinitionId.CreateUnique(), PolicyKey, Version + 1, Name, Description,
            string.IsNullOrWhiteSpace(owner) ? Owner : owner.Trim(), Domain, DefaultEffect,
            Scope, Environment, TenantId, PolicyStatus.Draft, 1, now, now, null,
            _rules.Select(rule => PolicyRule.Create(rule.Name, rule.Effect, rule.Priority,
                rule.Match.ToDictionary(item => item.Key, item => item.Value),
                rule.Constraints.ToDictionary(item => item.Key, item => item.Value))));
    }

    public void UpdateDraft(
        string name,
        string description,
        string owner,
        PolicyEffect defaultEffect,
        PolicyScope scope,
        string environment,
        Guid? tenantId)
    {
        EnsureDraft();
        ValidateMetadata(name, owner, Version, scope, tenantId);
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Owner = owner.Trim();
        DefaultEffect = defaultEffect;
        Scope = scope;
        Environment = string.IsNullOrWhiteSpace(environment) ? "All" : environment.Trim();
        TenantId = tenantId;
        Touch();
    }

    public void AddRule(PolicyRule rule)
    {
        EnsureDraft();
        if (_rules.Any(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Rule '{rule.Name}' already exists in policy '{Name}'.");
        _rules.Add(rule);
        Touch();
    }

    public void ReplaceRules(IEnumerable<PolicyRule> rules)
    {
        EnsureDraft();
        var replacement = rules.ToList();
        if (replacement.GroupBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Rule names must be unique within a policy version.");
        _rules.Clear();
        _rules.AddRange(replacement);
        Touch();
    }

    public void SubmitForApproval()
    {
        EnsureDraft();
        if (_rules.Count == 0 && DefaultEffect == PolicyEffect.Deny)
            throw new InvalidOperationException("A deny-by-default policy requires at least one allow rule before approval.");
        Status = PolicyStatus.PendingApproval;
        Touch();
    }

    public void Activate()
    {
        if (Status == PolicyStatus.Retired)
            throw new InvalidOperationException("A retired policy cannot be activated.");
        if (Status == PolicyStatus.Active) return;
        Status = PolicyStatus.Active;
        ActivatedAt = DateTimeOffset.UtcNow;
        Touch();
        AddDomainEvent(new PolicyActivatedEvent(Id, Name));
    }

    public void Suspend()
    {
        if (Status != PolicyStatus.Active)
            throw new InvalidOperationException("Only an active policy can be suspended.");
        Status = PolicyStatus.Suspended;
        Touch();
    }

    public void Retire()
    {
        if (Status == PolicyStatus.Retired) return;
        Status = PolicyStatus.Retired;
        Touch();
    }

    public PolicyDecision Evaluate(PolicyEvaluationContext context)
    {
        if (Status != PolicyStatus.Active)
            return PolicyDecision.Allow($"Policy '{Name}' is not active; abstained.", Id);

        if (context.Domain != Domain)
            return PolicyDecision.Allow($"Policy '{Name}' does not govern domain '{context.Domain}'; abstained.", Id);

        if (!AppliesTo(context))
            return PolicyDecision.Allow($"Policy '{Name}' does not apply to the supplied scope; abstained.", Id);

        var winning = _rules
            .Where(r => r.Matches(context))
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var decision = winning is null
            ? DecisionFor(DefaultEffect, $"No rule matched; default effect of policy '{Name}' applied.", null)
            : DecisionFor(winning.Effect, $"Rule '{winning.Name}' of policy '{Name}' applied.", winning);

        AddDomainEvent(new PolicyEvaluatedEvent(Id, context.Domain, decision.Effect));
        return decision;
    }

    private bool AppliesTo(PolicyEvaluationContext context) => Scope switch
    {
        PolicyScope.Global => true,
        PolicyScope.Environment => string.Equals(
            Environment,
            context.Get("environment") ?? "All",
            StringComparison.OrdinalIgnoreCase),
        PolicyScope.Tenant => TenantId.HasValue && TenantId == context.TenantId,
        _ => false
    };

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

    private void EnsureDraft()
    {
        if (Status != PolicyStatus.Draft)
            throw new InvalidOperationException("Only draft policy versions can be modified.");
    }

    private void Touch()
    {
        Revision++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateMetadata(string name, string owner, int version, PolicyScope scope, Guid? tenantId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Policy name is required.");
        if (name.Trim().Length > 200) throw new ArgumentException("Policy name cannot exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Policy owner is required.");
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (scope == PolicyScope.Tenant && !tenantId.HasValue)
            throw new ArgumentException("Tenant scoped policies require a tenant identifier.");
    }
}
