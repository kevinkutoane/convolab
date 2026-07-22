using ConvoLab.Domain.Policy.Enums;

namespace ConvoLab.Application.PolicyStudio;

public sealed record PolicyRuleInput(
    string Name,
    PolicyEffect Effect,
    int Priority,
    IReadOnlyDictionary<string, string>? Match,
    IReadOnlyDictionary<string, string>? Constraints);

public sealed record PolicyRuleDto(
    string Name,
    PolicyEffect Effect,
    int Priority,
    IReadOnlyDictionary<string, string> Match,
    IReadOnlyDictionary<string, string> Constraints);

public sealed record PolicySummaryDto(
    Guid Id,
    Guid PolicyKey,
    int Version,
    string Name,
    string Description,
    string Owner,
    PolicyDomain Domain,
    PolicyStatus Status,
    PolicyScope Scope,
    string Environment,
    Guid? TenantId,
    PolicyEffect DefaultEffect,
    int RuleCount,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ActivatedAt);

public sealed record PolicyDetailDto(
    PolicySummaryDto Summary,
    IReadOnlyList<PolicyRuleDto> Rules,
    IReadOnlyList<PolicyDecisionDto> RecentDecisions,
    IReadOnlyList<PolicySummaryDto> VersionHistory);

public sealed record PolicyDecisionDto(
    Guid Id,
    Guid? PolicyId,
    Guid? PolicyKey,
    int? PolicyVersion,
    string PolicyName,
    PolicyDomain Domain,
    PolicyEffect Effect,
    string Reason,
    IReadOnlyDictionary<string, string> Context,
    IReadOnlyDictionary<string, string> Constraints,
    string Source,
    string CorrelationId,
    Guid? SimulationId,
    Guid? RunId,
    DateTimeOffset CreatedAt);

public sealed record PolicyCoverageDto(
    PolicyDomain Domain,
    int ActivePolicies,
    int Decisions,
    int Denials,
    double DenyRate,
    string Status);

public sealed record PolicyMetricsDto(
    int LogicalPolicies,
    int PolicyVersions,
    int ActivePolicies,
    int DraftPolicies,
    int Decisions,
    int Denials,
    int ConstrainedDecisions,
    double DenyRate);

public sealed record PolicyOverviewDto(
    PolicyMetricsDto Metrics,
    IReadOnlyList<PolicySummaryDto> Policies,
    IReadOnlyList<PolicyDecisionDto> RecentDecisions,
    IReadOnlyList<PolicyCoverageDto> Coverage,
    IReadOnlyList<string> Environments,
    DateTimeOffset GeneratedAt);

public sealed record CreatePolicyCommand(
    string Name,
    string Description,
    string Owner,
    PolicyDomain Domain,
    PolicyEffect DefaultEffect,
    PolicyScope Scope,
    string Environment,
    Guid? TenantId,
    IReadOnlyList<PolicyRuleInput>? Rules);

public sealed record UpdatePolicyCommand(
    string Name,
    string Description,
    string Owner,
    PolicyEffect DefaultEffect,
    PolicyScope Scope,
    string Environment,
    Guid? TenantId,
    long Revision,
    IReadOnlyList<PolicyRuleInput>? Rules);

public sealed record CreatePolicyVersionCommand(string Owner);

public sealed record EvaluatePolicyCommand(
    PolicyDomain Domain,
    Guid? TenantId,
    IReadOnlyDictionary<string, string>? Attributes,
    string Source = "Manual",
    string? CorrelationId = null,
    Guid? SimulationId = null,
    Guid? RunId = null);

public sealed record PolicyEvaluationResultDto(
    PolicyEffect Effect,
    bool IsAllowed,
    string Reason,
    IReadOnlyDictionary<string, string> Constraints,
    IReadOnlyList<PolicyDecisionDto> Decisions,
    string CorrelationId,
    DateTimeOffset EvaluatedAt);

public sealed record PolicyExecutionRequest(
    string Provider,
    string Model,
    decimal EstimatedCost,
    string Currency,
    int RequestedMaxOutputTokens,
    bool AllowFallback,
    bool AllowStreaming,
    string Source,
    string Environment,
    Guid? SimulationId = null,
    Guid? RunId = null,
    Guid? TenantId = null);

public sealed record PolicyExecutionGuardrails(
    bool IsAllowed,
    string Reason,
    decimal MaxCostPerExecution,
    string Currency,
    int MaxOutputTokens,
    bool AllowFallback,
    bool AllowStreaming,
    IReadOnlyList<PolicyDecisionDto> Decisions,
    string CorrelationId);

public sealed record PolicyDefinitionState(
    Guid Id,
    Guid PolicyKey,
    int Version,
    string Name,
    string Description,
    string Owner,
    PolicyDomain Domain,
    PolicyStatus Status,
    PolicyScope Scope,
    string Environment,
    Guid? TenantId,
    PolicyEffect DefaultEffect,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ActivatedAt,
    IReadOnlyList<PolicyRuleState> Rules);

public sealed record PolicyRuleState(
    Guid Id,
    Guid PolicyId,
    string Name,
    PolicyEffect Effect,
    int Priority,
    IReadOnlyDictionary<string, string> Match,
    IReadOnlyDictionary<string, string> Constraints);

public sealed record PolicyDecisionState(
    Guid Id,
    Guid? PolicyId,
    Guid? PolicyKey,
    int? PolicyVersion,
    string PolicyName,
    PolicyDomain Domain,
    PolicyEffect Effect,
    string Reason,
    IReadOnlyDictionary<string, string> Context,
    IReadOnlyDictionary<string, string> Constraints,
    string Source,
    string CorrelationId,
    Guid? SimulationId,
    Guid? RunId,
    DateTimeOffset CreatedAt);

public sealed record PolicyVersionUpdate(
    PolicyDefinitionState State,
    long ExpectedRevision);

public interface IPolicyStudioRepository
{
    Task<int> CountPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyDefinitionState>> ListPoliciesAsync(CancellationToken cancellationToken = default);
    Task<PolicyDefinitionState?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyDefinitionState>> GetVersionHistoryAsync(Guid policyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyDefinitionState>> ListActiveByDomainAsync(PolicyDomain domain, Guid? tenantId, string environment, CancellationToken cancellationToken = default);
    Task AddPolicyAsync(PolicyDefinitionState policy, CancellationToken cancellationToken = default);
    Task UpdatePolicyAsync(PolicyDefinitionState policy, long expectedRevision, CancellationToken cancellationToken = default);
    Task ActivateVersionAsync(PolicyDefinitionState policy, long expectedRevision, IReadOnlyList<PolicyVersionUpdate> retiredVersions, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyDecisionState>> ListDecisionsAsync(int limit = 250, Guid? policyId = null, CancellationToken cancellationToken = default);
    Task AddDecisionAsync(PolicyDecisionState decision, CancellationToken cancellationToken = default);
}

public interface IPolicyDecisionService
{
    Task<PolicyEvaluationResultDto> EvaluateAsync(EvaluatePolicyCommand command, CancellationToken cancellationToken = default);
    Task<PolicyExecutionGuardrails> EvaluateExecutionAsync(PolicyExecutionRequest request, CancellationToken cancellationToken = default);
}

public interface IPolicyStudioService : IPolicyDecisionService
{
    Task<PolicyOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicySummaryDto>> ListPoliciesAsync(CancellationToken cancellationToken = default);
    Task<PolicyDetailDto> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PolicyDetailDto> CreatePolicyAsync(CreatePolicyCommand command, CancellationToken cancellationToken = default);
    Task<PolicyDetailDto> UpdatePolicyAsync(Guid id, UpdatePolicyCommand command, CancellationToken cancellationToken = default);
    Task<PolicyDetailDto> CreateVersionAsync(Guid id, CreatePolicyVersionCommand command, CancellationToken cancellationToken = default);
    Task<PolicyDetailDto> TransitionAsync(Guid id, string action, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyDecisionDto>> ListDecisionsAsync(int limit = 250, CancellationToken cancellationToken = default);
}
