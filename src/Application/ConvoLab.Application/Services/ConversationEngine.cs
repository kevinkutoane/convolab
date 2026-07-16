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
        conversation.Pause();
        await _repository.UpdateAsync(conversation);
    }

    public async Task ResumeConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Resume();
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

    public async Task RestoreConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.Restore();
        await _repository.UpdateAsync(conversation);
    }

    public async Task ExpireConversationAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.ExpireConversation();
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

    public async Task CloseInactiveSessionsAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.CloseInactiveSessions();
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

    public async Task AddMessageAsync(ConversationId conversationId, ParticipantRole role, MessageContent content, UserId senderId, MessageType type, ConversationMetadata? metadata = null)
    {
        var conversation = await GetConversationAsync(conversationId);
        var message = ConversationMessage.Create(
            role, 
            content, 
            senderId, 
            metadata ?? ConversationMetadata.Create(new Dictionary<string, string>()),
            type);
        conversation.AddMessage(message);
        await _repository.UpdateAsync(conversation);
    }

    public async Task AttachKnowledgeReferenceAsync(ConversationId conversationId, ConversationAttachment attachment, MessageId messageId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.AttachKnowledgeReference(attachment, messageId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task AttachWorkflowExecutionAsync(ConversationId conversationId, ExecutionId executionId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.AttachWorkflowExecution(executionId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task AttachEvaluationAsync(ConversationId conversationId, EvaluationId evaluationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.AttachEvaluation(evaluationId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task AttachTraceAsync(ConversationId conversationId, TraceId traceId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.AttachTrace(traceId);
        await _repository.UpdateAsync(conversation);
    }

    public async Task UpdateMemoryAsync(ConversationId conversationId, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type, bool isPinned = false, string? semanticReference = null)
    {
        var conversation = await GetConversationAsync(conversationId);
        var memory = ConversationMemory.Create(strategy, window, content, type, isPinned, semanticReference);
        conversation.UpdateMemory(memory);
        await _repository.UpdateAsync(conversation);
    }

    public async Task UpdateContextAsync(ConversationId conversationId, ConversationContext context)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.UpdateContext(context);
        await _repository.UpdateAsync(conversation);
    }

    public async Task CreateSnapshotAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.CreateSnapshot();
        await _repository.UpdateAsync(conversation);
    }

    public async Task RestoreSnapshotAsync(ConversationId conversationId, SnapshotId snapshotId)
    {
        var conversation = await GetConversationAsync(conversationId);
        conversation.RestoreSnapshot(snapshotId);
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

    public async Task<ConversationStatisticsDto> GetStatisticsAsync(ConversationId conversationId)
    {
        var conversation = await GetConversationAsync(conversationId);
        return new ConversationStatisticsDto(
            conversation.MessageCount,
            conversation.ParticipantCount,
            conversation.SessionCount,
            conversation.WorkflowCount,
            conversation.EvaluationCount,
            conversation.AttachmentCount,
            conversation.TimelineCount,
            conversation.TotalDuration,
            conversation.AverageResponseTime
        );
    }
}
