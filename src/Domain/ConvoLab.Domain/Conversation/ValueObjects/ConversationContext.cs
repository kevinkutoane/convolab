using ConvoLab.Domain.Common;
using ConvoLab.Domain.AI.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class ConversationContext : ValueObject
{
    public string? CurrentIntent { get; private set; }
    public WorkflowDefinitionId? CurrentWorkflowId { get; private set; }
    public SessionId? CurrentSessionId { get; private set; }
    public WorkflowStepId? CurrentStepId { get; private set; }
    public PromptTemplateId? CurrentPromptId { get; private set; }
    public AIProviderId? CurrentAIProviderId { get; private set; }
    public AIModelId? CurrentModelId { get; private set; }
    public IReadOnlyList<KnowledgeBaseId> KnowledgeReferences { get; private set; }
    public ConvoLab.Domain.Execution.ValueObjects.ExecutionContext? ExecutionContext { get; private set; }
    public ConversationWindow? ContextWindow { get; private set; }

    private ConversationContext(
        string? currentIntent,
        WorkflowDefinitionId? currentWorkflowId,
        SessionId? currentSessionId,
        WorkflowStepId? currentStepId,
        PromptTemplateId? currentPromptId,
        AIProviderId? currentAIProviderId,
        AIModelId? currentModelId,
        IEnumerable<KnowledgeBaseId> knowledgeReferences,
        ConvoLab.Domain.Execution.ValueObjects.ExecutionContext? executionContext,
        ConversationWindow? contextWindow)
    {
        CurrentIntent = currentIntent;
        CurrentWorkflowId = currentWorkflowId;
        CurrentSessionId = currentSessionId;
        CurrentStepId = currentStepId;
        CurrentPromptId = currentPromptId;
        CurrentAIProviderId = currentAIProviderId;
        CurrentModelId = currentModelId;
        KnowledgeReferences = knowledgeReferences.ToList().AsReadOnly();
        ExecutionContext = executionContext;
        ContextWindow = contextWindow;
    }

    public static ConversationContext Create(
        string? currentIntent = null,
        WorkflowDefinitionId? currentWorkflowId = null,
        SessionId? currentSessionId = null,
        WorkflowStepId? currentStepId = null,
        PromptTemplateId? currentPromptId = null,
        AIProviderId? currentAIProviderId = null,
        AIModelId? currentModelId = null,
        IEnumerable<KnowledgeBaseId>? knowledgeReferences = null,
        ConvoLab.Domain.Execution.ValueObjects.ExecutionContext? executionContext = null,
        ConversationWindow? contextWindow = null)
    {
        return new(
            currentIntent,
            currentWorkflowId,
            currentSessionId,
            currentStepId,
            currentPromptId,
            currentAIProviderId,
            currentModelId,
            knowledgeReferences ?? new List<KnowledgeBaseId>(),
            executionContext,
            contextWindow);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CurrentIntent ?? string.Empty;
        yield return CurrentWorkflowId ?? WorkflowDefinitionId.Create(Guid.Empty);
        yield return CurrentSessionId ?? SessionId.Create(Guid.Empty);
        yield return CurrentStepId ?? WorkflowStepId.Create(Guid.Empty);
        yield return CurrentPromptId ?? PromptTemplateId.FromGuid(Guid.Empty);
        yield return CurrentAIProviderId ?? AIProviderId.Create(Guid.Empty);
        yield return CurrentModelId ?? AIModelId.FromGuid(Guid.Empty);
        foreach (var kbId in KnowledgeReferences)
        {
            yield return kbId;
        }
        yield return ExecutionContext ?? ConvoLab.Domain.Execution.ValueObjects.ExecutionContext.Create(ExecutionId.FromGuid(Guid.Empty), WorkflowDefinitionId.Create(Guid.Empty).Value);
        yield return ContextWindow ?? ConversationWindow.Create(DateTime.MinValue);
    }

    private ConversationContext() { 
        KnowledgeReferences = new List<KnowledgeBaseId>().AsReadOnly();
    }
}

public class AIProviderId : ValueObject
{
    public Guid Value { get; private set; }

    private AIProviderId(Guid value)
    {
        Value = value;
    }

    public static AIProviderId CreateUnique() => new(Guid.NewGuid());

    public static AIProviderId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private AIProviderId() { }
}

public class WorkflowDefinitionId : ValueObject
{
    public Guid Value { get; private set; }

    private WorkflowDefinitionId(Guid value)
    {
        Value = value;
    }

    public static WorkflowDefinitionId CreateUnique() => new(Guid.NewGuid());

    public static WorkflowDefinitionId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private WorkflowDefinitionId() { }
}

public class WorkflowStepId : ValueObject
{
    public Guid Value { get; private set; }

    private WorkflowStepId(Guid value)
    {
        Value = value;
    }

    public static WorkflowStepId CreateUnique() => new(Guid.NewGuid());

    public static WorkflowStepId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private WorkflowStepId() { }
}
