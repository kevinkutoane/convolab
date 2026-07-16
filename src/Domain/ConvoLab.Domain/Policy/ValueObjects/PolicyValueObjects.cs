using ConvoLab.Domain.Common;
using ConvoLab.Domain.Policy.Enums;

namespace ConvoLab.Domain.Policy.ValueObjects;

/// <summary>Strongly-typed identifier for a PolicyDefinition aggregate.</summary>
public class PolicyDefinitionId : ValueObject
{
    public Guid Value { get; private set; }
    private PolicyDefinitionId(Guid value) => Value = value;
    private PolicyDefinitionId() { } // For EF Core
    public static PolicyDefinitionId CreateUnique() => new(Guid.NewGuid());
    public static PolicyDefinitionId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(PolicyDefinitionId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>
/// The subject and facts a policy decision is requested for. An opaque,
/// extensible attribute bag: engines put in what they know, rules read what
/// they need. No engine-specific types leak into the Policy context.
/// </summary>
public class PolicyEvaluationContext : ValueObject
{
    private readonly Dictionary<string, string> _attributes = new();

    public PolicyDomain Domain { get; private set; }
    public Guid? TenantId { get; private set; }
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    private PolicyEvaluationContext() { } // For EF Core

    private PolicyEvaluationContext(PolicyDomain domain, Guid? tenantId, IDictionary<string, string> attributes)
    {
        Domain = domain;
        TenantId = tenantId;
        _attributes = new Dictionary<string, string>(attributes);
    }

    public static PolicyEvaluationContext Create(PolicyDomain domain, Guid? tenantId = null, IDictionary<string, string>? attributes = null)
        => new(domain, tenantId, attributes ?? new Dictionary<string, string>());

    public string? Get(string key) => _attributes.TryGetValue(key, out var value) ? value : null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Domain;
        yield return TenantId ?? Guid.Empty;
        foreach (var kv in _attributes.OrderBy(k => k.Key))
        {
            yield return kv.Key;
            yield return kv.Value;
        }
    }
}

/// <summary>
/// The decision returned by the Policy Engine: effect, reason, and optional
/// constraints (e.g. "allowed, but max 1000 tokens"). Execution engines obey;
/// they never re-litigate.
/// </summary>
public class PolicyDecision : ValueObject
{
    private readonly Dictionary<string, string> _constraints = new();

    public PolicyEffect Effect { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public PolicyDefinitionId? DecidedBy { get; private set; }
    public IReadOnlyDictionary<string, string> Constraints => _constraints;

    public bool IsAllowed => Effect != PolicyEffect.Deny;

    private PolicyDecision() { } // For EF Core

    private PolicyDecision(PolicyEffect effect, string reason, PolicyDefinitionId? decidedBy, IDictionary<string, string> constraints)
    {
        Effect = effect;
        Reason = reason ?? string.Empty;
        DecidedBy = decidedBy;
        _constraints = new Dictionary<string, string>(constraints);
    }

    public static PolicyDecision Allow(string reason = "", PolicyDefinitionId? decidedBy = null)
        => new(PolicyEffect.Allow, reason, decidedBy, new Dictionary<string, string>());

    public static PolicyDecision AllowWithConstraints(IDictionary<string, string> constraints, string reason = "", PolicyDefinitionId? decidedBy = null)
        => new(PolicyEffect.AllowWithConstraints, reason, decidedBy, constraints);

    public static PolicyDecision Deny(string reason, PolicyDefinitionId? decidedBy = null)
        => new(PolicyEffect.Deny, reason, decidedBy, new Dictionary<string, string>());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Effect;
        yield return Reason;
        yield return DecidedBy?.Value ?? Guid.Empty;
        foreach (var kv in _constraints.OrderBy(k => k.Key))
        {
            yield return kv.Key;
            yield return kv.Value;
        }
    }
}

/// <summary>
/// A declarative rule inside a policy definition: when the context matches
/// (all match-attributes equal), the effect applies. Deliberately simple —
/// a rule engine can replace matching later without changing the contract.
/// </summary>
public class PolicyRule : ValueObject
{
    private readonly Dictionary<string, string> _match = new();
    private readonly Dictionary<string, string> _constraints = new();

    public string Name { get; private set; } = string.Empty;
    public PolicyEffect Effect { get; private set; }
    public int Priority { get; private set; }
    public IReadOnlyDictionary<string, string> Match => _match;
    public IReadOnlyDictionary<string, string> Constraints => _constraints;

    private PolicyRule() { } // For EF Core

    private PolicyRule(string name, PolicyEffect effect, int priority, IDictionary<string, string> match, IDictionary<string, string> constraints)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Rule name is required.");
        Name = name;
        Effect = effect;
        Priority = priority;
        _match = new Dictionary<string, string>(match);
        _constraints = new Dictionary<string, string>(constraints);
    }

    public static PolicyRule Create(string name, PolicyEffect effect, int priority = 0, IDictionary<string, string>? match = null, IDictionary<string, string>? constraints = null)
        => new(name, effect, priority, match ?? new Dictionary<string, string>(), constraints ?? new Dictionary<string, string>());

    /// <summary>True when every match attribute equals the context's value.</summary>
    public bool Matches(PolicyEvaluationContext context)
        => _match.All(kv => context.Get(kv.Key) == kv.Value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Effect;
        yield return Priority;
        foreach (var kv in _match.OrderBy(k => k.Key)) { yield return kv.Key; yield return kv.Value; }
        foreach (var kv in _constraints.OrderBy(k => k.Key)) { yield return kv.Key; yield return kv.Value; }
    }
}
