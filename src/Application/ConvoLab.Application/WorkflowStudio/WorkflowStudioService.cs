using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Application.WorkflowStudio;

public sealed class WorkflowStudioService(
    IWorkflowStudioRepository repository,
    IUnitOfWork unitOfWork) : IWorkflowStudioService
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> ListAsync(CancellationToken ct = default)
        => (await repository.ListAsync(ct))
            .Select(MapSummary)
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();

    public async Task<WorkflowDetailDto?> GetAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await repository.GetAsync(workflowId, ct);
        if (workflow is null) return null;
        return MapDetail(workflow, await repository.ListAuditAsync(workflowId, ct));
    }

    public async Task<WorkflowDetailDto> CreateAsync(CreateWorkflowCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw Validation("workflow.name.required", "Workflow name is required.", "name");

        var workflow = new WorkflowDefinition(
            Guid.NewGuid(),
            command.Name,
            command.Description,
            command.Owner,
            command.Tags);
        await repository.AddAsync(workflow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapDetail(workflow, []);
    }

    public async Task<WorkflowDetailDto?> UpdateAsync(Guid workflowId, UpdateWorkflowCommand command, CancellationToken ct = default)
    {
        var workflow = await repository.GetAsync(workflowId, ct);
        if (workflow is null) return null;
        var expected = command.ExpectedRevision;
        workflow.UpdateMetadata(command.Name, command.Description, command.Owner, command.Tags);
        await repository.UpdateAsync(workflow, expected, ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapDetail(workflow, await repository.ListAuditAsync(workflowId, ct));
    }

    public async Task<WorkflowVersionDto> CreateVersionAsync(Guid workflowId, CreateWorkflowVersionCommand command, CancellationToken ct = default)
    {
        var workflow = await repository.GetAsync(workflowId, ct)
            ?? throw new ResourceNotFoundException("workflow.not_found", $"Workflow '{workflowId}' was not found.");
        if (workflow.Revision != command.ExpectedWorkflowRevision)
            throw new ConcurrencyConflictException("workflow", workflowId);

        var (major, minor, patch) = ParseVersion(command.Version);
        WorkflowVersion version;
        try
        {
            version = workflow.CreateVersion(major, minor, patch);
            version.SetChangeSummary(command.ChangeSummary);
            ApplyGraph(version, command.Nodes, command.Transitions);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourceConflictException("workflow.version.invalid", exception.Message);
        }

        await repository.UpdateAsync(workflow, command.ExpectedWorkflowRevision, ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<WorkflowVersionDto?> UpdateGraphAsync(Guid versionId, UpdateWorkflowGraphCommand command, CancellationToken ct = default)
    {
        var workflow = await repository.GetByVersionIdAsync(versionId, ct);
        if (workflow is null) return null;
        var version = workflow.GetVersion(versionId);
        if (version.Revision != command.ExpectedRevision)
            throw new ConcurrencyConflictException("workflow version", versionId);

        try
        {
            foreach (var node in version.Nodes.ToList()) version.RemoveNode(node.Id);
            version.SetChangeSummary(command.ChangeSummary);
            ApplyGraph(version, command.Nodes, command.Transitions);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("workflow.graph.invalid", exception.Message);
        }

        var workflowRevision = workflow.Revision;
        await repository.UpdateAsync(workflow, workflowRevision, versionId, command.ExpectedRevision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<WorkflowVersionDto?> TransitionAsync(Guid versionId, string action, WorkflowLifecycleCommand command, CancellationToken ct = default)
    {
        var workflow = await repository.GetByVersionIdAsync(versionId, ct);
        if (workflow is null) return null;
        var version = workflow.GetVersion(versionId);
        if (version.Revision != command.ExpectedRevision)
            throw new ConcurrencyConflictException("workflow version", versionId);

        var previous = version.Status;
        var expectedWorkflowRevision = workflow.Revision;
        try
        {
            switch (action.Trim().ToLowerInvariant())
            {
                case "submit": version.Submit(command.Actor); break;
                case "approve": version.Approve(command.Actor); break;
                case "reject": version.Reject(command.Actor, command.Reason); break;
                case "publish": workflow.PublishVersion(versionId, command.Actor); break;
                case "deprecate": version.Deprecate(command.Actor, command.Reason); break;
                case "archive": version.Archive(command.Actor); break;
                case "restore": version.Restore(command.Actor); break;
                default: throw new RequestValidationException("workflow.action.invalid", $"Unsupported workflow action '{action}'.");
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("workflow.lifecycle.invalid_transition", exception.Message);
        }

        await repository.UpdateAsync(workflow, expectedWorkflowRevision, versionId, command.ExpectedRevision, ct);
        await repository.AddAuditAsync(new WorkflowAuditState(
            Guid.NewGuid(), version.Id, NormalizeActor(command.Actor), action.Trim().ToLowerInvariant(), command.Reason,
            previous, version.Status, DateTimeOffset.UtcNow), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<WorkflowVersionDto?> ValidateAsync(Guid versionId, CancellationToken ct = default)
    {
        var workflow = await repository.GetByVersionIdAsync(versionId, ct);
        return workflow is null ? null : MapVersion(workflow.GetVersion(versionId));
    }

    public async Task<IReadOnlyList<RuntimeWorkflowTemplate>> ListPublishedAsync(CancellationToken ct = default)
        => (await repository.ListAsync(ct))
            .Where(workflow => workflow.IsActive)
            .SelectMany(workflow => workflow.Versions
                .Where(version => version.Status == WorkflowLifecycleStatus.Published)
                .Select(version => MapRuntime(workflow, version)))
            .OrderBy(item => item.Name)
            .ThenByDescending(item => item.Version)
            .ToList();

    public async Task<RuntimeWorkflowTemplate?> ResolvePublishedAsync(string displayName, CancellationToken ct = default)
        => (await ListPublishedAsync(ct)).FirstOrDefault(item =>
            item.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

    private static void ApplyGraph(
        WorkflowVersion version,
        IReadOnlyList<WorkflowNodeInput> nodes,
        IReadOnlyList<WorkflowTransitionInput> transitions)
    {
        if (nodes.Count == 0)
            throw new InvalidOperationException("A workflow version must contain nodes.");

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var input in nodes)
        {
            var original = input.Id ?? Guid.NewGuid();
            var actual = original == Guid.Empty ? Guid.NewGuid() : original;
            idMap[original] = actual;
            version.AddNode(actual, input.Name, input.Kind, input.PositionX, input.PositionY, input.Configuration);
        }

        foreach (var input in transitions)
        {
            var from = idMap.TryGetValue(input.FromNodeId, out var mappedFrom) ? mappedFrom : input.FromNodeId;
            var to = idMap.TryGetValue(input.ToNodeId, out var mappedTo) ? mappedTo : input.ToNodeId;
            var transitionId = !input.Id.HasValue || input.Id.Value == Guid.Empty ? Guid.NewGuid() : input.Id.Value;
            version.AddTransition(transitionId, from, to, input.Label, input.Condition);
        }
    }

    private static (int Major, int Minor, int Patch) ParseVersion(string value)
    {
        var parts = (value ?? string.Empty).Trim().Split('.');
        if (parts.Length != 3 || !parts.All(part => int.TryParse(part, out var number) && number >= 0))
            throw Validation("workflow.version.invalid", "Version must use MAJOR.MINOR.PATCH format.", "version");
        return (int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    private static RequestValidationException Validation(string code, string message, string field)
        => new(code, message, new Dictionary<string, string[]> { [field] = [message] });

    private static string NormalizeActor(string actor) => string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();

    private static WorkflowSummaryDto MapSummary(WorkflowDefinition workflow)
    {
        var latest = workflow.Versions.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ThenByDescending(v => v.Patch).FirstOrDefault();
        var published = workflow.Versions.FirstOrDefault(v => v.Status == WorkflowLifecycleStatus.Published);
        return new WorkflowSummaryDto(
            workflow.Id, workflow.Name, workflow.Description, workflow.Owner, workflow.Tags.ToList(), workflow.IsActive,
            published?.Status ?? latest?.Status ?? WorkflowLifecycleStatus.Draft,
            latest?.VersionString ?? "—", workflow.Versions.Count,
            new DateTimeOffset(workflow.LastModifiedAt ?? workflow.CreatedAt, TimeSpan.Zero), workflow.Revision);
    }

    private static WorkflowDetailDto MapDetail(WorkflowDefinition workflow, IReadOnlyList<WorkflowAuditState> audit)
        => new(
            workflow.Id, workflow.Name, workflow.Description, workflow.Owner, workflow.Tags.ToList(), workflow.IsActive,
            workflow.Versions.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ThenByDescending(v => v.Patch).Select(MapVersion).ToList(),
            audit.Select(item => new WorkflowAuditDto(item.Id, item.WorkflowVersionId, item.Actor, item.Action, item.Reason, item.PreviousStatus, item.NewStatus, item.CreatedAt)).ToList(),
            new DateTimeOffset(workflow.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(workflow.LastModifiedAt ?? workflow.CreatedAt, TimeSpan.Zero),
            workflow.Revision);

    private static WorkflowVersionDto MapVersion(WorkflowVersion version)
    {
        var issues = version.ValidateGraph();
        return new WorkflowVersionDto(
            version.Id, version.WorkflowDefinitionId, version.VersionString, version.Status, version.ChangeSummary,
            version.Nodes.Select(node => new WorkflowNodeDto(node.Id, node.Name, node.Kind, node.PositionX, node.PositionY, node.Configuration)).ToList(),
            version.Transitions.Select(item => new WorkflowTransitionDto(item.Id, item.FromNodeId, item.ToNodeId, item.Label, item.Condition)).ToList(),
            issues.Select(issue => new WorkflowValidationIssueDto(issue.Code, issue.Message, issue.NodeId)).ToList(),
            issues.Count == 0, version.ApprovedBy, version.ApprovedAt, version.PublishedAt,
            new DateTimeOffset(version.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(version.LastModifiedAt ?? version.CreatedAt, TimeSpan.Zero), version.Revision);
    }

    private static RuntimeWorkflowTemplate MapRuntime(WorkflowDefinition workflow, WorkflowVersion version)
        => new(
            workflow.Id, version.Id, workflow.Name, version.VersionString, $"{workflow.Name} v{version.VersionString}",
            version.Nodes.Select(node => new WorkflowNodeDto(node.Id, node.Name, node.Kind, node.PositionX, node.PositionY, node.Configuration)).ToList(),
            version.Transitions.Select(item => new WorkflowTransitionDto(item.Id, item.FromNodeId, item.ToNodeId, item.Label, item.Condition)).ToList());
}
