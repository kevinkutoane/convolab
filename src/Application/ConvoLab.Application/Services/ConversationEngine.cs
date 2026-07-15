using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.Entities;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.Interfaces;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;

namespace ConvoLab.Application.Services;

public class ConversationEngine : IConversationEngine
{
    private readonly IConversationRepository _repository;

    public ConversationEngine(IConversationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConversationId> CreateConversationAsync(UserId creatorId, string title, ConversationMetadata? metadata = null)
    {
        var conversation = Conversation.Create(
            creatorId, 
            title, 
            metadata ?? ConversationMetadata.Create(new Dictionary<string, string>()), 
            ConversationWindow.Create(DateTime.UtcNow), 
            ConversationContext.Create());
            
        await _repository.AddAsync(conversation);
        return conversation.Id;
    }

    public async Task StartConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Start();
        await _repository.UpdateAsync(conversation);
    }

    public async Task PauseConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Wait();
        await _repository.UpdateAsync(conversation);
    }

    public async Task ResumeConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Activate();
        await _repository.UpdateAsync(conversation);
    }

    public async Task CompleteConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Complete();
        await _repository.UpdateAsync(conversation);
    }

    public async Task ArchiveConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Archive();
        await _repository.UpdateAsync(conversation);
    }

    public async Task StartSessionAsync(ConversationId conversationId, IEnumerable<ParticipantId> participantIds, ConversationMetadata? metadata = null)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.StartSession(participantIds, metadata);
        await _repository.UpdateAsync(conversation);
    }

    public async Task EndSessionAsync(ConversationId conversationId, SessionId sessionId, SessionStatus status, string? reason = null)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.EndSession(sessionId, status, reason);
        await _repository.UpdateAsync(conversation);
    }

    public async Task AddParticipantAsync(ConversationId conversationId, UserId userId, ParticipantRole role)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.AddParticipant(userId, role);
        await _repository.UpdateAsync(conversation);
    }

    public async Task RemoveParticipantAsync(ConversationId conversationId, ParticipantId participantId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.RemoveParticipant(participantId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task AddMessageAsync(ConversationId conversationId, ParticipantRole role, string content, ParticipantId senderId, ConversationMetadata? metadata = null)
    {
        var conversation = await GetConversationAsync(conversationId);
        var message = ConversationMessage.Create(role, content, senderId, metadata);
        conversation.AddMessage(message);
        await _repository.UpdateAsync(conversation);
    }

    public async Task LinkWorkflowAsync(ConversationId conversationId, ExecutionId executionId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.LinkWorkflow(executionId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task LinkEvaluationAsync(ConversationId conversationId, EvaluationId evaluationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.LinkEvaluation(evaluationId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task LinkTraceAsync(ConversationId conversationId, TraceId traceId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.LinkTrace(traceId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task UpdateMemoryAsync(ConversationId conversationId, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type)
    {
        var conversation = await GetConversationAsync(conversationId);
        var memory = ConversationMemory.Create(strategy, window, content, type);
        conversation.UpdateMemory(memory);
        await _repository.UpdateAsync(conversation);
    }

    public async Task TakeSnapshotAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.TakeSnapshot();
        await _repository.UpdateAsync(conversation);
    }

    public async Task<Conversation> GetConversationAsync(ConversationId conversationId)
    {
        var conversation = await _repository.GetByIdAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException($"Conversation {conversationId.Value} not found.");
        return conversation;
    }

    public async Task<ConversationTimeline> GetTimelineAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        return conversation.Timeline;
    }
}
