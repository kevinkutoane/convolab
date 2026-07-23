using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.WorkflowStudio;
using ConvoLab.Domain.Execution.Aggregates;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.WorkflowStudio;

public sealed class EfWorkflowStudioRepository(ApplicationDbContext db) : IWorkflowStudioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken ct = default)
    {
        var ids = await db.Workflows.AsNoTracking().OrderByDescending(item => item.UpdatedAt).Select(item => item.Id).ToListAsync(ct);
        var result = new List<WorkflowDefinition>(ids.Count);
        foreach (var id in ids)
        {
            var item = await GetAsync(id, ct);
            if (item is not null) result.Add(item);
        }
        return result;
    }

    public async Task<WorkflowDefinition?> GetAsync(Guid workflowId, CancellationToken ct = default)
    {
        var record = await db.Workflows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == workflowId, ct);
        if (record is null) return null;

        var versionRecords = await db.WorkflowVersions.AsNoTracking()
            .Where(item => item.WorkflowId == workflowId)
            .ToListAsync(ct);
        var versionIds = versionRecords.Select(item => item.Id).ToList();
        var nodeRecords = await db.WorkflowNodes.AsNoTracking().Where(item => versionIds.Contains(item.WorkflowVersionId)).ToListAsync(ct);
        var transitionRecords = await db.WorkflowTransitions.AsNoTracking().Where(item => versionIds.Contains(item.WorkflowVersionId)).ToListAsync(ct);

        var versions = versionRecords.Select(version => WorkflowVersion.Rehydrate(
            version.Id,
            version.WorkflowId,
            version.Major,
            version.Minor,
            version.Patch,
            Enum.Parse<WorkflowLifecycleStatus>(version.Status, true),
            version.ChangeSummary,
            version.ApprovedBy,
            version.ApprovedAt,
            version.PublishedAt,
            version.Revision,
            version.CreatedAt,
            version.UpdatedAt,
            nodeRecords.Where(item => item.WorkflowVersionId == version.Id).Select(MapNode),
            transitionRecords.Where(item => item.WorkflowVersionId == version.Id).Select(MapTransition)))
            .ToList();

        return WorkflowDefinition.Rehydrate(
            record.Id,
            record.Name,
            record.Description,
            record.Owner,
            Deserialize<string[]>(record.TagsJson) ?? [],
            record.IsActive,
            record.Revision,
            record.CreatedAt,
            record.UpdatedAt,
            versions);
    }

    public async Task<WorkflowDefinition?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default)
    {
        var workflowId = await db.WorkflowVersions.AsNoTracking()
            .Where(item => item.Id == versionId && db.Workflows.Any(workflow => workflow.Id == item.WorkflowId))
            .Select(item => (Guid?)item.WorkflowId)
            .SingleOrDefaultAsync(ct);
        return workflowId.HasValue ? await GetAsync(workflowId.Value, ct) : null;
    }

    public Task AddAsync(WorkflowDefinition workflow, CancellationToken ct = default)
    {
        db.Workflows.Add(MapWorkflow(workflow));
        AddChildren(workflow);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(
        WorkflowDefinition workflow,
        long expectedWorkflowRevision,
        Guid? expectedVersionId = null,
        long? expectedVersionRevision = null,
        CancellationToken ct = default)
    {
        var record = await db.Workflows.SingleOrDefaultAsync(item => item.Id == workflow.Id, ct)
            ?? throw new ResourceNotFoundException("workflow.not_found", $"Workflow '{workflow.Id}' was not found.");
        if (record.Revision != expectedWorkflowRevision)
            throw new ConcurrencyConflictException("workflow", workflow.Id);

        if (expectedVersionId.HasValue && expectedVersionRevision.HasValue)
        {
            var persistedVersionRevision = await db.WorkflowVersions.AsNoTracking()
                .Where(item => item.Id == expectedVersionId.Value)
                .Select(item => (long?)item.Revision)
                .SingleOrDefaultAsync(ct);
            if (!persistedVersionRevision.HasValue)
                throw new ResourceNotFoundException("workflow.version.not_found", $"Workflow version '{expectedVersionId}' was not found.");
            if (persistedVersionRevision.Value != expectedVersionRevision.Value)
                throw new ConcurrencyConflictException("workflow version", expectedVersionId.Value);
        }

        record.Name = workflow.Name;
        record.Description = workflow.Description;
        record.Owner = workflow.Owner;
        record.TagsJson = JsonSerializer.Serialize(workflow.Tags, JsonOptions);
        record.IsActive = workflow.IsActive;
        record.Revision = workflow.Revision;
        record.UpdatedAt = workflow.LastModifiedAt;

        var existingVersions = await db.WorkflowVersions.Where(item => item.WorkflowId == workflow.Id).ToListAsync(ct);
        foreach (var version in workflow.Versions)
        {
            var versionRecord = existingVersions.SingleOrDefault(item => item.Id == version.Id);
            if (versionRecord is null)
            {
                versionRecord = MapVersion(version);
                db.WorkflowVersions.Add(versionRecord);
            }
            else
            {
                Apply(versionRecord, version);
            }

            var existingNodes = await db.WorkflowNodes.Where(item => item.WorkflowVersionId == version.Id).ToListAsync(ct);
            foreach (var node in version.Nodes)
            {
                var nodeRecord = existingNodes.SingleOrDefault(item => item.Id == node.Id);
                if (nodeRecord is null) db.WorkflowNodes.Add(MapNodeRecord(node));
                else Apply(nodeRecord, node);
            }
            db.WorkflowNodes.RemoveRange(existingNodes.Where(record => version.Nodes.All(node => node.Id != record.Id)));

            var existingTransitions = await db.WorkflowTransitions.Where(item => item.WorkflowVersionId == version.Id).ToListAsync(ct);
            foreach (var transition in version.Transitions)
            {
                var transitionRecord = existingTransitions.SingleOrDefault(item => item.Id == transition.Id);
                if (transitionRecord is null) db.WorkflowTransitions.Add(MapTransitionRecord(transition));
                else Apply(transitionRecord, transition);
            }
            db.WorkflowTransitions.RemoveRange(existingTransitions.Where(record => version.Transitions.All(transition => transition.Id != record.Id)));
        }
    }

    public Task AddAuditAsync(WorkflowAuditState entry, CancellationToken ct = default)
    {
        db.WorkflowAudit.Add(new WorkflowAuditRecord
        {
            Id = entry.Id,
            WorkflowVersionId = entry.WorkflowVersionId,
            Actor = entry.Actor,
            Action = entry.Action,
            Reason = entry.Reason,
            PreviousStatus = entry.PreviousStatus.ToString(),
            NewStatus = entry.NewStatus.ToString(),
            CreatedAt = entry.CreatedAt
        });
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<WorkflowAuditState>> ListAuditAsync(Guid workflowId, CancellationToken ct = default)
    {
        var versionIds = await db.WorkflowVersions.AsNoTracking().Where(item => item.WorkflowId == workflowId).Select(item => item.Id).ToListAsync(ct);
        var records = await db.WorkflowAudit.AsNoTracking()
            .Where(item => versionIds.Contains(item.WorkflowVersionId))
            .ToListAsync(ct);
        return records.OrderByDescending(item => item.CreatedAt).Select(item => new WorkflowAuditState(
            item.Id,
            item.WorkflowVersionId,
            item.Actor,
            item.Action,
            item.Reason,
            Enum.Parse<WorkflowLifecycleStatus>(item.PreviousStatus, true),
            Enum.Parse<WorkflowLifecycleStatus>(item.NewStatus, true),
            item.CreatedAt)).ToList();
    }

    private void AddChildren(WorkflowDefinition workflow)
    {
        db.WorkflowVersions.AddRange(workflow.Versions.Select(MapVersion));
        db.WorkflowNodes.AddRange(workflow.Versions.SelectMany(item => item.Nodes).Select(MapNodeRecord));
        db.WorkflowTransitions.AddRange(workflow.Versions.SelectMany(item => item.Transitions).Select(MapTransitionRecord));
    }

    private static WorkflowRecord MapWorkflow(WorkflowDefinition workflow) => new()
    {
        Id = workflow.Id,
        Name = workflow.Name,
        Description = workflow.Description,
        Owner = workflow.Owner,
        TagsJson = JsonSerializer.Serialize(workflow.Tags, JsonOptions),
        IsActive = workflow.IsActive,
        Revision = workflow.Revision,
        CreatedAt = workflow.CreatedAt,
        UpdatedAt = workflow.LastModifiedAt
    };

    private static WorkflowVersionRecord MapVersion(WorkflowVersion version)
    {
        var record = new WorkflowVersionRecord { Id = version.Id, WorkflowId = version.WorkflowDefinitionId };
        Apply(record, version);
        return record;
    }

    private static void Apply(WorkflowVersionRecord record, WorkflowVersion version)
    {
        record.Major = version.Major;
        record.Minor = version.Minor;
        record.Patch = version.Patch;
        record.Status = version.Status.ToString();
        record.ChangeSummary = version.ChangeSummary;
        record.ApprovedBy = version.ApprovedBy;
        record.ApprovedAt = version.ApprovedAt;
        record.PublishedAt = version.PublishedAt;
        record.Revision = version.Revision;
        record.CreatedAt = version.CreatedAt;
        record.UpdatedAt = version.LastModifiedAt;
    }


    private static void Apply(WorkflowNodeRecord record, WorkflowNode node)
    {
        record.Name = node.Name;
        record.Kind = node.Kind.ToString();
        record.PositionX = node.PositionX;
        record.PositionY = node.PositionY;
        record.ConfigurationJson = JsonSerializer.Serialize(node.Configuration, JsonOptions);
        record.UpdatedAt = node.LastModifiedAt;
    }

    private static void Apply(WorkflowTransitionRecord record, WorkflowTransition transition)
    {
        record.FromNodeId = transition.FromNodeId;
        record.ToNodeId = transition.ToNodeId;
        record.Label = transition.Label;
        record.Condition = transition.Condition;
    }

    private static WorkflowNodeRecord MapNodeRecord(WorkflowNode node) => new()
    {
        Id = node.Id,
        WorkflowVersionId = node.WorkflowVersionId,
        Name = node.Name,
        Kind = node.Kind.ToString(),
        PositionX = node.PositionX,
        PositionY = node.PositionY,
        ConfigurationJson = JsonSerializer.Serialize(node.Configuration, JsonOptions),
        CreatedAt = node.CreatedAt,
        UpdatedAt = node.LastModifiedAt
    };

    private static WorkflowNode MapNode(WorkflowNodeRecord node)
        => new(
            node.Id,
            node.WorkflowVersionId,
            node.Name,
            Enum.Parse<WorkflowNodeKind>(node.Kind, true),
            node.PositionX,
            node.PositionY,
            Deserialize<Dictionary<string, string>>(node.ConfigurationJson));

    private static WorkflowTransitionRecord MapTransitionRecord(WorkflowTransition transition) => new()
    {
        Id = transition.Id,
        WorkflowVersionId = transition.WorkflowVersionId,
        FromNodeId = transition.FromNodeId,
        ToNodeId = transition.ToNodeId,
        Label = transition.Label,
        Condition = transition.Condition,
        CreatedAt = transition.CreatedAt
    };

    private static WorkflowTransition MapTransition(WorkflowTransitionRecord transition)
        => new(
            transition.Id,
            transition.WorkflowVersionId,
            transition.FromNodeId,
            transition.ToNodeId,
            transition.Label,
            transition.Condition);

    private static T? Deserialize<T>(string json)
        => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
}
