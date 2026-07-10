using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.AI.ValueObjects;

namespace ConvoLab.Domain.Execution.ValueObjects;

public class ExecutionContext : ValueObject
{
    public ConversationId? ConversationId { get; private set; }
    public UserId? UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public PromptTemplateId? CurrentPromptTemplateId { get; private set; }
    public string? RetrievedKnowledgeContext { get; private set; }
    public string? SelectedAIProvider { get; private set; }
    public AIModelId? SelectedAIModelId { get; private set; }
    public IReadOnlyDictionary<string, string> Metadata { get; private set; }
    public IReadOnlyDictionary<string, string> ExecutionVariables { get; private set; }
    public CancellationToken CancellationToken { get; private set; }

    private ExecutionContext(
        Guid correlationId,
        ConversationId? conversationId = null,
        UserId? userId = null,
        Guid? tenantId = null,
        PromptTemplateId? currentPromptTemplateId = null,
        string? retrievedKnowledgeContext = null,
        string? selectedAIProvider = null,
        AIModelId? selectedAIModelId = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyDictionary<string, string>? executionVariables = null,
        CancellationToken cancellationToken = default)
    {
        CorrelationId = correlationId;
        ConversationId = conversationId;
        UserId = userId;
        TenantId = tenantId;
        CurrentPromptTemplateId = currentPromptTemplateId;
        RetrievedKnowledgeContext = retrievedKnowledgeContext;
        SelectedAIProvider = selectedAIProvider;
        SelectedAIModelId = selectedAIModelId;
        Metadata = metadata ?? new Dictionary<string, string>();
        ExecutionVariables = executionVariables ?? new Dictionary<string, string>();
        CancellationToken = cancellationToken;
    }

    public static ExecutionContext Create(
        Guid correlationId,
        ConversationId? conversationId = null,
        UserId? userId = null,
        Guid? tenantId = null,
        PromptTemplateId? currentPromptTemplateId = null,
        string? retrievedKnowledgeContext = null,
        string? selectedAIProvider = null,
        AIModelId? selectedAIModelId = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyDictionary<string, string>? executionVariables = null,
        CancellationToken cancellationToken = default)
    {
        return new ExecutionContext(
            correlationId,
            conversationId,
            userId,
            tenantId,
            currentPromptTemplateId,
            retrievedKnowledgeContext,
            selectedAIProvider,
            selectedAIModelId,
            metadata,
            executionVariables,
            cancellationToken);
    }

    public ExecutionContext WithConversationId(ConversationId conversationId) =>
        new(CorrelationId, conversationId, UserId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithUserId(UserId userId) =>
        new(CorrelationId, ConversationId, userId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithTenantId(Guid tenantId) =>
        new(CorrelationId, ConversationId, UserId, tenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithCurrentPromptTemplateId(PromptTemplateId promptTemplateId) =>
        new(CorrelationId, ConversationId, UserId, TenantId, promptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithRetrievedKnowledgeContext(string knowledgeContext) =>
        new(CorrelationId, ConversationId, UserId, TenantId, CurrentPromptTemplateId, knowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithSelectedAIProvider(string aiProvider) =>
        new(CorrelationId, ConversationId, UserId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, aiProvider, SelectedAIModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithSelectedAIModelId(AIModelId aiModelId) =>
        new(CorrelationId, ConversationId, UserId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, aiModelId, Metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithMetadata(IReadOnlyDictionary<string, string> metadata) =>
        new(CorrelationId, ConversationId, UserId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, metadata, ExecutionVariables, CancellationToken);

    public ExecutionContext WithExecutionVariables(IReadOnlyDictionary<string, string> executionVariables) =>
        new(CorrelationId, ConversationId, UserId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, executionVariables, CancellationToken);

    public ExecutionContext WithCancellationToken(CancellationToken cancellationToken) =>
        new(CorrelationId, ConversationId, UserId, TenantId, CurrentPromptTemplateId, RetrievedKnowledgeContext, SelectedAIProvider, SelectedAIModelId, Metadata, ExecutionVariables, cancellationToken);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CorrelationId;
        yield return ConversationId ?? new ConversationId(Guid.Empty);
        yield return UserId ?? new UserId(Guid.Empty);
        yield return TenantId ?? Guid.Empty;
        yield return CurrentPromptTemplateId ?? new PromptTemplateId(Guid.Empty);
        yield return RetrievedKnowledgeContext ?? string.Empty;
        yield return SelectedAIProvider ?? string.Empty;
        yield return SelectedAIModelId ?? new AIModelId(Guid.Empty);
    }

    // For EF Core
    private ExecutionContext() { 
        Metadata = new Dictionary<string, string>();
        ExecutionVariables = new Dictionary<string, string>();
    }
}
