namespace ConvoLab.Infrastructure.PolicyStudio;

public sealed class PolicyDefinitionRecord
{
    public Guid Id { get; set; }
    public Guid PolicyKey { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string DefaultEffect { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
}

public sealed class PolicyRuleRecord
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string MatchJson { get; set; } = "{}";
    public string ConstraintsJson { get; set; } = "{}";
}

public sealed class PolicyDecisionRecord
{
    public Guid Id { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? PolicyKey { get; set; }
    public int? PolicyVersion { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ContextJson { get; set; } = "{}";
    public string ConstraintsJson { get; set; } = "{}";
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? SimulationId { get; set; }
    public Guid? RunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
