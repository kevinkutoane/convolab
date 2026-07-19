using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Application.WorkflowStudio;

public sealed record WorkflowAuditState(
    Guid Id,
    Guid WorkflowVersionId,
    string Actor,
    string Action,
    string? Reason,
    WorkflowLifecycleStatus PreviousStatus,
    WorkflowLifecycleStatus NewStatus,
    DateTimeOffset CreatedAt);

public interface IWorkflowStudioRepository
{
    Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken ct = default);
    Task<WorkflowDefinition?> GetAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default);
    Task AddAsync(WorkflowDefinition workflow, CancellationToken ct = default);
    Task UpdateAsync(
        WorkflowDefinition workflow,
        long expectedWorkflowRevision,
        Guid? expectedVersionId = null,
        long? expectedVersionRevision = null,
        CancellationToken ct = default);
    Task AddAuditAsync(WorkflowAuditState entry, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowAuditState>> ListAuditAsync(Guid workflowId, CancellationToken ct = default);
}
