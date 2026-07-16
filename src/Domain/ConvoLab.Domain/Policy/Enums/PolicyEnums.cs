namespace ConvoLab.Domain.Policy.Enums;

/// <summary>The outcome of a policy evaluation.</summary>
public enum PolicyEffect
{
    Allow,
    AllowWithConstraints,
    Deny
}

/// <summary>
/// Governance domains the Policy Engine can rule on. New domains are one-line
/// additions — policy evolves independently of the execution engines.
/// </summary>
public enum PolicyDomain
{
    ModelAccess,
    ProviderAccess,
    BudgetLimit,
    RateLimit,
    PromptApproval,
    KnowledgeAccess,
    TenantRule,
    Compliance,
    EvaluationThreshold,
    Safety
}

/// <summary>Lifecycle of a policy definition.</summary>
public enum PolicyStatus
{
    Draft,
    Active,
    Suspended,
    Retired
}
