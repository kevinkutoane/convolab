using ConvoLab.Domain.Common;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Execution.Aggregates;

public class WorkflowDefinition : BaseAggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsActive { get; private set; }
    
    private readonly List<WorkflowVersion> _versions = new();
    public IReadOnlyCollection<WorkflowVersion> Versions => _versions.AsReadOnly();

    public WorkflowDefinition(Guid id, string name, string description) : base(id)
    {
        Name = name;
        Description = description;
        IsActive = true;
    }

    public WorkflowVersion CreateVersion(int major, int minor, int patch)
    {
        var version = new WorkflowVersion(Guid.NewGuid(), Id, major, minor, patch);
        _versions.Add(version);
        return version;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    // For EF Core
    private WorkflowDefinition() { 
        Name = null!;
        Description = null!;
    }
}

public class WorkflowVersion : BaseEntity<Guid>
{
    public Guid WorkflowDefinitionId { get; private set; }
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public int Patch { get; private set; }
    
    private readonly List<WorkflowNode> _nodes = new();
    public IReadOnlyCollection<WorkflowNode> Nodes => _nodes.AsReadOnly();

    public WorkflowVersion(Guid id, Guid workflowDefinitionId, int major, int minor, int patch) : base(id)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public void AddNode(string name, string type, Dictionary<string, string>? config = null)
    {
        _nodes.Add(new WorkflowNode(Guid.NewGuid(), Id, name, type, config));
    }

    public string VersionString => $"{Major}.{Minor}.{Patch}";

    // For EF Core
    private WorkflowVersion() { }
}

public class WorkflowNode : BaseEntity<Guid>
{
    public Guid WorkflowVersionId { get; private set; }
    public string Name { get; private set; }
    public string Type { get; private set; }
    public IReadOnlyDictionary<string, string> Configuration { get; private set; }

    public WorkflowNode(Guid id, Guid workflowVersionId, string name, string type, Dictionary<string, string>? configuration = null) : base(id)
    {
        WorkflowVersionId = workflowVersionId;
        Name = name;
        Type = type;
        Configuration = configuration ?? new Dictionary<string, string>();
    }

    // For EF Core
    private WorkflowNode() { 
        Name = null!;
        Type = null!;
        Configuration = new Dictionary<string, string>();
    }
}
