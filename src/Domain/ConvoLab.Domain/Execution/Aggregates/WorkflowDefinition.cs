using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Execution.Aggregates;

public enum WorkflowLifecycleStatus
{
    Draft,
    PendingApproval,
    Approved,
    Published,
    Deprecated,
    Archived
}

public enum WorkflowNodeKind
{
    Start,
    Prompt,
    Knowledge,
    Decision,
    Intelligence,
    Response,
    End
}

public sealed record WorkflowValidationIssue(string Code, string Message, Guid? NodeId = null);

public class WorkflowDefinition : BaseAggregateRoot<Guid>
{
    private readonly List<WorkflowVersion> _versions = new();

    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Owner { get; private set; }
    public IReadOnlyCollection<string> Tags { get; private set; }
    public bool IsActive { get; private set; }
    public long Revision { get; private set; }
    public IReadOnlyCollection<WorkflowVersion> Versions => _versions.AsReadOnly();

    public WorkflowDefinition(Guid id, string name, string description)
        : this(id, name, description, "Unassigned", [], true, 1)
    {
    }

    public WorkflowDefinition(
        Guid id,
        string name,
        string description,
        string owner,
        IEnumerable<string>? tags = null,
        bool isActive = true,
        long revision = 1)
        : base(id)
    {
        Name = Required(name, nameof(name));
        Description = description?.Trim() ?? string.Empty;
        Owner = string.IsNullOrWhiteSpace(owner) ? "Unassigned" : owner.Trim();
        Tags = NormalizeTags(tags);
        IsActive = isActive;
        Revision = Math.Max(1, revision);
    }

    public void UpdateMetadata(string name, string description, string owner, IEnumerable<string>? tags)
    {
        Name = Required(name, nameof(name));
        Description = description?.Trim() ?? string.Empty;
        Owner = string.IsNullOrWhiteSpace(owner) ? Owner : owner.Trim();
        Tags = NormalizeTags(tags);
        Revision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    public WorkflowVersion CreateVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0)
            throw new ArgumentOutOfRangeException(nameof(major), "Workflow version components cannot be negative.");

        var versionString = $"{major}.{minor}.{patch}";
        if (_versions.Any(item => item.VersionString == versionString))
            throw new InvalidOperationException($"Workflow version '{versionString}' already exists.");

        var version = new WorkflowVersion(Guid.NewGuid(), Id, major, minor, patch);
        _versions.Add(version);
        Revision++;
        LastModifiedAt = DateTime.UtcNow;
        return version;
    }

    public void AddRehydratedVersion(WorkflowVersion version)
    {
        if (version.WorkflowDefinitionId != Id)
            throw new InvalidOperationException("Workflow version belongs to another definition.");
        if (_versions.Any(item => item.Id == version.Id || item.VersionString == version.VersionString))
            throw new InvalidOperationException("Duplicate workflow version detected during rehydration.");
        _versions.Add(version);
    }

    public WorkflowVersion GetVersion(Guid versionId)
        => _versions.SingleOrDefault(item => item.Id == versionId)
           ?? throw new InvalidOperationException($"Workflow version '{versionId}' was not found.");

    public void PublishVersion(Guid versionId, string actor)
    {
        var version = GetVersion(versionId);
        foreach (var published in _versions.Where(item => item.Status == WorkflowLifecycleStatus.Published && item.Id != versionId))
            published.Deprecate(actor, "Superseded by a newly published version.");
        version.Publish(actor);
        Revision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        Revision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        Revision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    public static WorkflowDefinition Rehydrate(
        Guid id,
        string name,
        string description,
        string owner,
        IEnumerable<string>? tags,
        bool isActive,
        long revision,
        DateTime createdAt,
        DateTime? lastModifiedAt,
        IEnumerable<WorkflowVersion> versions)
    {
        var definition = new WorkflowDefinition(id, name, description, owner, tags, isActive, revision)
        {
            CreatedAt = createdAt,
            LastModifiedAt = lastModifiedAt
        };
        foreach (var version in versions.OrderByDescending(item => item.Major).ThenByDescending(item => item.Minor).ThenByDescending(item => item.Patch))
            definition.AddRehydratedVersion(version);
        return definition;
    }

    private static string Required(string value, string parameter)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();

    private static IReadOnlyCollection<string> NormalizeTags(IEnumerable<string>? tags)
        => (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private WorkflowDefinition()
    {
        Name = null!;
        Description = null!;
        Owner = null!;
        Tags = [];
    }
}

public class WorkflowVersion : BaseEntity<Guid>
{
    private readonly List<WorkflowNode> _nodes = new();
    private readonly List<WorkflowTransition> _transitions = new();

    public Guid WorkflowDefinitionId { get; private set; }
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public int Patch { get; private set; }
    public WorkflowLifecycleStatus Status { get; private set; }
    public string ChangeSummary { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public long Revision { get; private set; }
    public IReadOnlyCollection<WorkflowNode> Nodes => _nodes.AsReadOnly();
    public IReadOnlyCollection<WorkflowTransition> Transitions => _transitions.AsReadOnly();
    public string VersionString => $"{Major}.{Minor}.{Patch}";

    public WorkflowVersion(Guid id, Guid workflowDefinitionId, int major, int minor, int patch)
        : this(id, workflowDefinitionId, major, minor, patch, WorkflowLifecycleStatus.Draft, string.Empty, null, null, null, 1)
    {
    }

    private WorkflowVersion(
        Guid id,
        Guid workflowDefinitionId,
        int major,
        int minor,
        int patch,
        WorkflowLifecycleStatus status,
        string changeSummary,
        string? approvedBy,
        DateTimeOffset? approvedAt,
        DateTimeOffset? publishedAt,
        long revision)
        : base(id)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        Major = major;
        Minor = minor;
        Patch = patch;
        Status = status;
        ChangeSummary = changeSummary ?? string.Empty;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        PublishedAt = publishedAt;
        Revision = Math.Max(1, revision);
    }

    public void SetChangeSummary(string summary)
    {
        EnsureDraft();
        ChangeSummary = summary?.Trim() ?? string.Empty;
        Touch();
    }

    public WorkflowNode AddNode(string name, string type, Dictionary<string, string>? config = null)
    {
        if (!Enum.TryParse<WorkflowNodeKind>(type, true, out var kind))
            throw new ArgumentException($"Unsupported workflow node type '{type}'.", nameof(type));
        return AddNode(Guid.NewGuid(), name, kind, _nodes.Count * 240 + 80, 160, config);
    }

    public WorkflowNode AddNode(
        Guid id,
        string name,
        WorkflowNodeKind kind,
        double positionX,
        double positionY,
        IReadOnlyDictionary<string, string>? configuration = null)
    {
        EnsureDraft();
        if (_nodes.Any(item => item.Id == id))
            throw new InvalidOperationException($"Workflow node '{id}' already exists.");
        var node = new WorkflowNode(id, Id, name, kind, positionX, positionY, configuration);
        _nodes.Add(node);
        Touch();
        return node;
    }

    public void UpdateNode(
        Guid nodeId,
        string name,
        WorkflowNodeKind kind,
        double positionX,
        double positionY,
        IReadOnlyDictionary<string, string>? configuration = null)
    {
        EnsureDraft();
        var node = GetNode(nodeId);
        node.Update(name, kind, positionX, positionY, configuration);
        Touch();
    }

    public void RemoveNode(Guid nodeId)
    {
        EnsureDraft();
        var node = GetNode(nodeId);
        _nodes.Remove(node);
        _transitions.RemoveAll(item => item.FromNodeId == nodeId || item.ToNodeId == nodeId);
        Touch();
    }

    public WorkflowTransition AddTransition(
        Guid id,
        Guid fromNodeId,
        Guid toNodeId,
        string? label = null,
        string? condition = null)
    {
        EnsureDraft();
        _ = GetNode(fromNodeId);
        _ = GetNode(toNodeId);
        if (fromNodeId == toNodeId)
            throw new InvalidOperationException("A workflow transition cannot target the same node.");
        if (_transitions.Any(item => item.Id == id || (item.FromNodeId == fromNodeId && item.ToNodeId == toNodeId && item.Label == (label ?? string.Empty).Trim())))
            throw new InvalidOperationException("Duplicate workflow transition.");
        var transition = new WorkflowTransition(id, Id, fromNodeId, toNodeId, label, condition);
        _transitions.Add(transition);
        Touch();
        return transition;
    }

    public void RemoveTransition(Guid transitionId)
    {
        EnsureDraft();
        var transition = _transitions.SingleOrDefault(item => item.Id == transitionId)
            ?? throw new InvalidOperationException($"Workflow transition '{transitionId}' was not found.");
        _transitions.Remove(transition);
        Touch();
    }

    public IReadOnlyList<WorkflowValidationIssue> ValidateGraph()
    {
        var issues = new List<WorkflowValidationIssue>();
        var starts = _nodes.Where(item => item.Kind == WorkflowNodeKind.Start).ToList();
        var ends = _nodes.Where(item => item.Kind == WorkflowNodeKind.End).ToList();

        if (starts.Count != 1)
            issues.Add(new("workflow.start.count", "A workflow must contain exactly one Start node."));
        if (ends.Count == 0)
            issues.Add(new("workflow.end.required", "A workflow must contain at least one End node."));

        foreach (var transition in _transitions)
        {
            if (_nodes.All(node => node.Id != transition.FromNodeId))
                issues.Add(new("workflow.transition.source_missing", "A transition references a missing source node."));
            if (_nodes.All(node => node.Id != transition.ToNodeId))
                issues.Add(new("workflow.transition.target_missing", "A transition references a missing target node."));
        }

        foreach (var decision in _nodes.Where(item => item.Kind == WorkflowNodeKind.Decision))
        {
            if (_transitions.Count(item => item.FromNodeId == decision.Id) < 2)
                issues.Add(new("workflow.decision.branches", "A Decision node must have at least two outgoing transitions.", decision.Id));
        }

        if (starts.Count == 1)
        {
            var reachable = new HashSet<Guid> { starts[0].Id };
            var queue = new Queue<Guid>();
            queue.Enqueue(starts[0].Id);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var target in _transitions.Where(item => item.FromNodeId == current).Select(item => item.ToNodeId))
                {
                    if (reachable.Add(target)) queue.Enqueue(target);
                }
            }

            foreach (var node in _nodes.Where(item => !reachable.Contains(item.Id)))
                issues.Add(new("workflow.node.unreachable", $"Node '{node.Name}' is unreachable from Start.", node.Id));
        }

        foreach (var node in _nodes.Where(item => item.Kind != WorkflowNodeKind.End))
        {
            if (_transitions.All(item => item.FromNodeId != node.Id))
                issues.Add(new("workflow.node.dead_end", $"Node '{node.Name}' has no outgoing transition.", node.Id));
        }

        return issues;
    }

    public void Submit(string actor)
    {
        EnsureStatus(WorkflowLifecycleStatus.Draft);
        EnsureValid();
        Status = WorkflowLifecycleStatus.PendingApproval;
        LastModifiedBy = NormalizeActor(actor);
        Touch();
    }

    public void Approve(string actor)
    {
        EnsureStatus(WorkflowLifecycleStatus.PendingApproval);
        Status = WorkflowLifecycleStatus.Approved;
        ApprovedBy = NormalizeActor(actor);
        ApprovedAt = DateTimeOffset.UtcNow;
        LastModifiedBy = ApprovedBy;
        Touch();
    }

    public void Reject(string actor, string? reason = null)
    {
        EnsureStatus(WorkflowLifecycleStatus.PendingApproval);
        Status = WorkflowLifecycleStatus.Draft;
        ApprovedBy = null;
        ApprovedAt = null;
        LastModifiedBy = NormalizeActor(actor);
        if (!string.IsNullOrWhiteSpace(reason)) ChangeSummary = reason.Trim();
        Touch();
    }

    public void Publish(string actor)
    {
        EnsureStatus(WorkflowLifecycleStatus.Approved);
        EnsureValid();
        Status = WorkflowLifecycleStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        LastModifiedBy = NormalizeActor(actor);
        Touch();
    }

    public void Deprecate(string actor, string? reason = null)
    {
        EnsureStatus(WorkflowLifecycleStatus.Published);
        Status = WorkflowLifecycleStatus.Deprecated;
        LastModifiedBy = NormalizeActor(actor);
        if (!string.IsNullOrWhiteSpace(reason)) ChangeSummary = reason.Trim();
        Touch();
    }

    public void Archive(string actor)
    {
        if (Status is not (WorkflowLifecycleStatus.Draft or WorkflowLifecycleStatus.Deprecated))
            throw new InvalidOperationException($"Workflow version in '{Status}' cannot be archived.");
        Status = WorkflowLifecycleStatus.Archived;
        LastModifiedBy = NormalizeActor(actor);
        Touch();
    }

    public void Restore(string actor)
    {
        EnsureStatus(WorkflowLifecycleStatus.Archived);
        Status = WorkflowLifecycleStatus.Draft;
        LastModifiedBy = NormalizeActor(actor);
        Touch();
    }

    public static WorkflowVersion Rehydrate(
        Guid id,
        Guid workflowDefinitionId,
        int major,
        int minor,
        int patch,
        WorkflowLifecycleStatus status,
        string changeSummary,
        string? approvedBy,
        DateTimeOffset? approvedAt,
        DateTimeOffset? publishedAt,
        long revision,
        DateTime createdAt,
        DateTime? lastModifiedAt,
        IEnumerable<WorkflowNode> nodes,
        IEnumerable<WorkflowTransition> transitions)
    {
        var version = new WorkflowVersion(id, workflowDefinitionId, major, minor, patch, status, changeSummary, approvedBy, approvedAt, publishedAt, revision)
        {
            CreatedAt = createdAt,
            LastModifiedAt = lastModifiedAt
        };
        version._nodes.AddRange(nodes);
        version._transitions.AddRange(transitions);
        return version;
    }

    private WorkflowNode GetNode(Guid nodeId)
        => _nodes.SingleOrDefault(item => item.Id == nodeId)
           ?? throw new InvalidOperationException($"Workflow node '{nodeId}' was not found.");

    private void EnsureDraft()
    {
        if (Status != WorkflowLifecycleStatus.Draft)
            throw new InvalidOperationException("Only Draft workflow versions may be edited.");
    }

    private void EnsureStatus(WorkflowLifecycleStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Workflow version must be '{expected}' but is '{Status}'.");
    }

    private void EnsureValid()
    {
        var issues = ValidateGraph();
        if (issues.Count > 0)
            throw new InvalidOperationException($"Workflow graph is invalid: {string.Join(" ", issues.Select(issue => issue.Message))}");
    }

    private void Touch()
    {
        Revision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();

    private WorkflowVersion()
    {
        ChangeSummary = string.Empty;
    }
}

public class WorkflowNode : BaseEntity<Guid>
{
    public Guid WorkflowVersionId { get; private set; }
    public string Name { get; private set; }
    public WorkflowNodeKind Kind { get; private set; }
    public string Type => Kind.ToString();
    public double PositionX { get; private set; }
    public double PositionY { get; private set; }
    public IReadOnlyDictionary<string, string> Configuration { get; private set; }

    public WorkflowNode(
        Guid id,
        Guid workflowVersionId,
        string name,
        string type,
        Dictionary<string, string>? configuration = null)
        : this(
            id,
            workflowVersionId,
            name,
            Enum.TryParse<WorkflowNodeKind>(type, true, out var kind) ? kind : WorkflowNodeKind.Prompt,
            0,
            0,
            configuration)
    {
    }

    public WorkflowNode(
        Guid id,
        Guid workflowVersionId,
        string name,
        WorkflowNodeKind kind,
        double positionX,
        double positionY,
        IReadOnlyDictionary<string, string>? configuration = null)
        : base(id)
    {
        WorkflowVersionId = workflowVersionId;
        Name = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name.Trim();
        Kind = kind;
        PositionX = positionX;
        PositionY = positionY;
        Configuration = new Dictionary<string, string>(configuration ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
    }

    public void Update(
        string name,
        WorkflowNodeKind kind,
        double positionX,
        double positionY,
        IReadOnlyDictionary<string, string>? configuration)
    {
        Name = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name.Trim();
        Kind = kind;
        PositionX = positionX;
        PositionY = positionY;
        Configuration = new Dictionary<string, string>(configuration ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        LastModifiedAt = DateTime.UtcNow;
    }

    private WorkflowNode()
    {
        Name = null!;
        Configuration = new Dictionary<string, string>();
    }
}

public class WorkflowTransition : BaseEntity<Guid>
{
    public Guid WorkflowVersionId { get; private set; }
    public Guid FromNodeId { get; private set; }
    public Guid ToNodeId { get; private set; }
    public string Label { get; private set; }
    public string? Condition { get; private set; }

    public WorkflowTransition(
        Guid id,
        Guid workflowVersionId,
        Guid fromNodeId,
        Guid toNodeId,
        string? label = null,
        string? condition = null)
        : base(id)
    {
        WorkflowVersionId = workflowVersionId;
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        Label = label?.Trim() ?? string.Empty;
        Condition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();
    }

    private WorkflowTransition()
    {
        Label = string.Empty;
    }
}
