namespace ConvoLab.Application.Simulation;

public sealed class SimulationState
{
    private readonly object _gate = new();
    private readonly List<SimulationMessage> _messages = new();
    private readonly List<SimulationRun> _runs = new();

    public SimulationState(
        Guid id,
        string title,
        string workflow,
        string promptVersion,
        string knowledgeCollection,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Workflow = workflow;
        PromptVersion = promptVersion;
        KnowledgeCollection = knowledgeCollection;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }


    public static SimulationState FromSnapshot(SimulationConversation snapshot)
    {
        var state = new SimulationState(snapshot.Id, snapshot.Title, snapshot.Workflow, snapshot.PromptVersion, snapshot.KnowledgeCollection, snapshot.CreatedAt);
        state._messages.AddRange(snapshot.Messages);
        state._runs.AddRange(snapshot.Runs);
        state.UpdatedAt = snapshot.UpdatedAt;
        return state;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Workflow { get; }
    public string PromptVersion { get; }
    public string KnowledgeCollection { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public SimulationMessage AddMessage(string role, string content, bool isReplay = false)
    {
        lock (_gate)
        {
            var message = new SimulationMessage(Guid.NewGuid(), role, content, isReplay, DateTimeOffset.UtcNow);
            _messages.Add(message);
            UpdatedAt = message.CreatedAt;
            return message;
        }
    }

    public void AddRun(SimulationRun run)
    {
        lock (_gate)
        {
            _runs.Add(run);
            UpdatedAt = run.CreatedAt;
        }
    }

    public SimulationRun? FindRun(Guid runId)
    {
        lock (_gate)
        {
            return _runs.FirstOrDefault(run => run.Id == runId);
        }
    }

    public SimulationMessage? FindMessage(Guid messageId)
    {
        lock (_gate)
        {
            return _messages.FirstOrDefault(message => message.Id == messageId);
        }
    }

    public SimulationConversation Snapshot()
    {
        lock (_gate)
        {
            return new SimulationConversation(
                Id,
                Title,
                "Active",
                Workflow,
                PromptVersion,
                KnowledgeCollection,
                _messages.ToList(),
                _runs.ToList(),
                CreatedAt,
                UpdatedAt);
        }
    }

    public SimulationSummary Summary()
    {
        lock (_gate)
        {
            return new SimulationSummary(
                Id,
                Title,
                "Active",
                Workflow,
                PromptVersion,
                KnowledgeCollection,
                _messages.Count,
                _runs.Count,
                _messages.LastOrDefault()?.Content,
                CreatedAt,
                UpdatedAt);
        }
    }
}

