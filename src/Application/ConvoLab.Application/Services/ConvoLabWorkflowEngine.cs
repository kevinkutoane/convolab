using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.AI.ValueObjects;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;
namespace ConvoLab.Application.Services;
public class ConvoLabWorkflowEngine : IWorkflowEngine {
    private readonly IConversationEngine _conversationEngine;
    private readonly IPromptEngine _promptEngine;
    private readonly IKnowledgeEngine _knowledgeEngine;
    private readonly IAIOrchestrator _aiOrchestrator;
    private readonly ITraceEngine _traceEngine;
    private readonly IEvaluationEngine _evaluationEngine;
    private readonly IPluginManager _pluginManager;
    public ConvoLabWorkflowEngine(IConversationEngine conversationEngine, IPromptEngine promptEngine, IKnowledgeEngine knowledgeEngine, IAIOrchestrator aiOrchestrator, ITraceEngine traceEngine, IEvaluationEngine evaluationEngine, IPluginManager pluginManager) {
        _conversationEngine = conversationEngine; _promptEngine = promptEngine; _knowledgeEngine = knowledgeEngine; _aiOrchestrator = aiOrchestrator; _traceEngine = traceEngine; _evaluationEngine = evaluationEngine; _pluginManager = pluginManager;
    }
    public async Task<string> ProcessUserMessageAsync(ConversationId conversationId, UserId userId, string userMessage, CancellationToken cancellationToken = default) {
        var traceId = await _traceEngine.StartTraceAsync("ProcessUserMessage", cancellationToken);
        string conversationResponse = "";
        try {
            await _traceEngine.AddSpanToTraceAsync(traceId, "AddUserMessageToConversation", null, new Dictionary<string, string> { { "userMessage", userMessage } }, cancellationToken);
            await _conversationEngine.AddMessageToConversationAsync(conversationId, userId, userMessage, cancellationToken);
            var knowledgeItems = await _knowledgeEngine.SearchKnowledgeAsync(new KnowledgeBaseId(Guid.Empty), userMessage, cancellationToken);
            var knowledgeContext = string.Join("\n", knowledgeItems.Select(item => item.Content));
            var promptTemplateId = new PromptTemplateId(Guid.Empty);
            var promptParameters = new Dictionary<string, string> { { "userMessage", userMessage }, { "knowledgeContext", knowledgeContext } };
            var aiPrompt = await _promptEngine.GeneratePromptAsync(promptTemplateId, promptParameters, cancellationToken);
            var aiModelId = new AIModelId(Guid.Empty);
            conversationResponse = await _aiOrchestrator.ProcessPromptAsync(aiModelId, promptTemplateId, promptParameters, cancellationToken);
            await _conversationEngine.AddMessageToConversationAsync(conversationId, new UserId(Guid.Empty), conversationResponse, cancellationToken);
            var evaluationId = await _evaluationEngine.StartEvaluationAsync(conversationId, cancellationToken);
            await _evaluationEngine.AddEvaluationResultAsync(evaluationId, "ResponseQuality", "Good", "AI response was relevant and coherent.", cancellationToken);
            await _evaluationEngine.CompleteEvaluationAsync(evaluationId, cancellationToken);
            await _traceEngine.EndTraceAsync(traceId, cancellationToken);
        } catch (Exception ex) {
            await _traceEngine.FailTraceAsync(traceId, ex.Message, cancellationToken);
            throw;
        }
        return conversationResponse;
    }
}
