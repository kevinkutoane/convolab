using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.AI.ValueObjects;

namespace ConvoLab.Domain.Execution.ValueObjects;

public class ExecutionContext : ValueObject
{
    public ExecutionId ExecutionId { get; private set; }
    public ConversationId? ConversationId { get; private set; }
    public Guid? WorkflowId { get; private set; }
    public Guid? TenantId { get; private set; }
    public UserId? UserId { get; private set; }
    public Guid CorrelationId { get; private set; }
    
    public string? Culture { get; private set; }
    public string? Locale { get; private set; }
    public string? Timezone { get; private set; }
    
    public IReadOnlyList<string> FeatureFlags { get; private set; }
    public string? SelectedProvider { get; private set; }
    public AIModelId? SelectedModel { get; private set; }
    
    public IReadOnlyDictionary<string, string> ExecutionVariables { get; private set; }
    public string? MemoryReference { get; private set; }
    public string? PromptReference { get; private set; }
    public string? KnowledgeReference { get; private set; }
    
    public IReadOnlyDictionary<string, string> Metadata { get; private set; }
    public IReadOnlyList<string> Attachments { get; private set; }
    
    public DateTime ExecutionStartTime { get; private set; }
    public DateTime? ExecutionDeadline { get; private set; }

    private ExecutionContext(
        ExecutionId executionId,
        Guid correlationId,
        ConversationId? conversationId = null,
        Guid? workflowId = null,
        Guid? tenantId = null,
        UserId? userId = null,
        string? culture = null,
        string? locale = null,
        string? timezone = null,
        IEnumerable<string>? featureFlags = null,
        string? selectedProvider = null,
        AIModelId? selectedModel = null,
        IReadOnlyDictionary<string, string>? executionVariables = null,
        string? memoryReference = null,
        string? promptReference = null,
        string? knowledgeReference = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IEnumerable<string>? attachments = null,
        DateTime? executionStartTime = null,
        DateTime? executionDeadline = null)
    {
        ExecutionId = executionId;
        CorrelationId = correlationId;
        ConversationId = conversationId;
        WorkflowId = workflowId;
        TenantId = tenantId;
        UserId = userId;
        Culture = culture;
        Locale = locale;
        Timezone = timezone;
        FeatureFlags = featureFlags?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        SelectedProvider = selectedProvider;
        SelectedModel = selectedModel;
        ExecutionVariables = executionVariables ?? new Dictionary<string, string>();
        MemoryReference = memoryReference;
        PromptReference = promptReference;
        KnowledgeReference = knowledgeReference;
        Metadata = metadata ?? new Dictionary<string, string>();
        Attachments = attachments?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        ExecutionStartTime = executionStartTime ?? DateTime.UtcNow;
        ExecutionDeadline = executionDeadline;
    }

    public static ExecutionContext Create(ExecutionId executionId, Guid correlationId)
    {
        return new ExecutionContext(executionId, correlationId);
    }

    // Wither patterns for immutability
    public ExecutionContext WithConversationId(ConversationId conversationId) => 
        new(ExecutionId, CorrelationId, conversationId, WorkflowId, TenantId, UserId, Culture, Locale, Timezone, FeatureFlags, SelectedProvider, SelectedModel, ExecutionVariables, MemoryReference, PromptReference, KnowledgeReference, Metadata, Attachments, ExecutionStartTime, ExecutionDeadline);

    public ExecutionContext WithWorkflowId(Guid workflowId) => 
        new(ExecutionId, CorrelationId, ConversationId, workflowId, TenantId, UserId, Culture, Locale, Timezone, FeatureFlags, SelectedProvider, SelectedModel, ExecutionVariables, MemoryReference, PromptReference, KnowledgeReference, Metadata, Attachments, ExecutionStartTime, ExecutionDeadline);

    public ExecutionContext WithTenantId(Guid tenantId) => 
        new(ExecutionId, CorrelationId, ConversationId, WorkflowId, tenantId, UserId, Culture, Locale, Timezone, FeatureFlags, SelectedProvider, SelectedModel, ExecutionVariables, MemoryReference, PromptReference, KnowledgeReference, Metadata, Attachments, ExecutionStartTime, ExecutionDeadline);

    public ExecutionContext WithUserId(UserId userId) => 
        new(ExecutionId, CorrelationId, ConversationId, WorkflowId, TenantId, userId, Culture, Locale, Timezone, FeatureFlags, SelectedProvider, SelectedModel, ExecutionVariables, MemoryReference, PromptReference, KnowledgeReference, Metadata, Attachments, ExecutionStartTime, ExecutionDeadline);

    public ExecutionContext WithSelectedModel(AIModelId modelId) => 
        new(ExecutionId, CorrelationId, ConversationId, WorkflowId, TenantId, UserId, Culture, Locale, Timezone, FeatureFlags, SelectedProvider, modelId, ExecutionVariables, MemoryReference, PromptReference, KnowledgeReference, Metadata, Attachments, ExecutionStartTime, ExecutionDeadline);

    public ExecutionContext WithVariables(IReadOnlyDictionary<string, string> variables) => 
        new(ExecutionId, CorrelationId, ConversationId, WorkflowId, TenantId, UserId, Culture, Locale, Timezone, FeatureFlags, SelectedProvider, SelectedModel, variables, MemoryReference, PromptReference, KnowledgeReference, Metadata, Attachments, ExecutionStartTime, ExecutionDeadline);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ExecutionId;
        yield return CorrelationId;
        if (ConversationId != null) yield return ConversationId;
        if (WorkflowId != null) yield return WorkflowId;
        if (TenantId != null) yield return TenantId;
        if (UserId != null) yield return UserId;
    }

    // For EF Core
    private ExecutionContext() { 
        ExecutionId = null!;
        FeatureFlags = new List<string>().AsReadOnly();
        ExecutionVariables = new Dictionary<string, string>();
        Metadata = new Dictionary<string, string>();
        Attachments = new List<string>().AsReadOnly();
    }
}
