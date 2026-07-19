using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Application.WorkflowStudio;

public sealed record WorkflowNodeInput(
    Guid? Id,
    string Name,
    WorkflowNodeKind Kind,
    double PositionX,
    double PositionY,
    IReadOnlyDictionary<string, string>? Configuration);

public sealed record WorkflowTransitionInput(
    Guid? Id,
    Guid FromNodeId,
    Guid ToNodeId,
    string? Label,
    string? Condition);

public sealed record WorkflowNodeDto(
    Guid Id,
    string Name,
    WorkflowNodeKind Kind,
    double PositionX,
    double PositionY,
    IReadOnlyDictionary<string, string> Configuration);

public sealed record WorkflowTransitionDto(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    string Label,
    string? Condition);

public sealed record WorkflowValidationIssueDto(
    string Code,
    string Message,
    Guid? NodeId);

public sealed record WorkflowVersionDto(
    Guid Id,
    Guid WorkflowId,
    string Version,
    WorkflowLifecycleStatus Status,
    string ChangeSummary,
    IReadOnlyList<WorkflowNodeDto> Nodes,
    IReadOnlyList<WorkflowTransitionDto> Transitions,
    IReadOnlyList<WorkflowValidationIssueDto> ValidationIssues,
    bool IsValid,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record WorkflowSummaryDto(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    IReadOnlyList<string> Tags,
    bool IsActive,
    WorkflowLifecycleStatus Status,
    string LatestVersion,
    int VersionCount,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record WorkflowDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    IReadOnlyList<string> Tags,
    bool IsActive,
    IReadOnlyList<WorkflowVersionDto> Versions,
    IReadOnlyList<WorkflowAuditDto> Audit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record WorkflowAuditDto(
    Guid Id,
    Guid WorkflowVersionId,
    string Actor,
    string Action,
    string? Reason,
    WorkflowLifecycleStatus PreviousStatus,
    WorkflowLifecycleStatus NewStatus,
    DateTimeOffset CreatedAt);

public sealed record CreateWorkflowCommand(
    string Name,
    string Description,
    string Owner,
    IReadOnlyList<string>? Tags);

public sealed record UpdateWorkflowCommand(
    string Name,
    string Description,
    string Owner,
    IReadOnlyList<string>? Tags,
    long ExpectedRevision);

public sealed record CreateWorkflowVersionCommand(
    string Version,
    string ChangeSummary,
    IReadOnlyList<WorkflowNodeInput> Nodes,
    IReadOnlyList<WorkflowTransitionInput> Transitions,
    long ExpectedWorkflowRevision);

public sealed record UpdateWorkflowGraphCommand(
    IReadOnlyList<WorkflowNodeInput> Nodes,
    IReadOnlyList<WorkflowTransitionInput> Transitions,
    string ChangeSummary,
    long ExpectedRevision);

public sealed record WorkflowLifecycleCommand(
    string Actor,
    string? Reason,
    long ExpectedRevision);

public sealed record RuntimeWorkflowTemplate(
    Guid WorkflowId,
    Guid VersionId,
    string Name,
    string Version,
    string DisplayName,
    IReadOnlyList<WorkflowNodeDto> Nodes,
    IReadOnlyList<WorkflowTransitionDto> Transitions);

public interface IWorkflowStudioService
{
    Task<IReadOnlyList<WorkflowSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<WorkflowDetailDto?> GetAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowDetailDto> CreateAsync(CreateWorkflowCommand command, CancellationToken ct = default);
    Task<WorkflowDetailDto?> UpdateAsync(Guid workflowId, UpdateWorkflowCommand command, CancellationToken ct = default);
    Task<WorkflowVersionDto> CreateVersionAsync(Guid workflowId, CreateWorkflowVersionCommand command, CancellationToken ct = default);
    Task<WorkflowVersionDto?> UpdateGraphAsync(Guid versionId, UpdateWorkflowGraphCommand command, CancellationToken ct = default);
    Task<WorkflowVersionDto?> TransitionAsync(Guid versionId, string action, WorkflowLifecycleCommand command, CancellationToken ct = default);
    Task<WorkflowVersionDto?> ValidateAsync(Guid versionId, CancellationToken ct = default);
    Task<IReadOnlyList<RuntimeWorkflowTemplate>> ListPublishedAsync(CancellationToken ct = default);
    Task<RuntimeWorkflowTemplate?> ResolvePublishedAsync(string displayName, CancellationToken ct = default);
}
