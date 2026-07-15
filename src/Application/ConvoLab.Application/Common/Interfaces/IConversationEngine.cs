using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.Entities;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;

namespace ConvoLab.Application.Common.Interfaces;

public interface IConversationEngine
{
    Task<ConversationId> CreateConversationAsync(UserId creatorId, string title, ConversationMetadata? metadata = null);
    Task StartConversationAsync(ConversationId conversationId);
    Task PauseConversationAsync(ConversationId conversationId);
    Task ResumeConversationAsync(ConversationId conversationId);
    Task CompleteConversationAsync(ConversationId conversationId);
    Task ArchiveConversationAsync(ConversationId conversationId);
    
    Task StartSessionAsync(ConversationId conversationId, IEnumerable<ParticipantId> participantIds, ConversationMetadata? metadata = null);
    Task EndSessionAsync(ConversationId conversationId, SessionId sessionId, SessionStatus status, string? reason = null);
    
    Task AddParticipantAsync(ConversationId conversationId, UserId userId, ParticipantRole role);
    Task RemoveParticipantAsync(ConversationId conversationId, ParticipantId participantId);
    
    Task AddMessageAsync(ConversationId conversationId, ParticipantRole role, string content, ParticipantId senderId, ConversationMetadata? metadata = null);
    
    Task LinkWorkflowAsync(ConversationId conversationId, ExecutionId executionId);
    Task LinkEvaluationAsync(ConversationId conversationId, EvaluationId evaluationId);
    Task LinkTraceAsync(ConversationId conversationId, TraceId traceId);
    
    Task UpdateMemoryAsync(ConversationId conversationId, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type);
    Task TakeSnapshotAsync(ConversationId conversationId);
    
    Task<Conversation> GetConversationAsync(ConversationId conversationId);
    Task<ConversationTimeline> GetTimelineAsync(ConversationId conversationId);
}
