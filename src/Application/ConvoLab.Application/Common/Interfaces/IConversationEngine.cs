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
    // Lifecycle
    Task<ConversationId> CreateConversationAsync(UserId creatorId, string title, ConversationMetadata? metadata = null);
    Task StartConversationAsync(ConversationId conversationId);
    Task PauseConversationAsync(ConversationId conversationId);
    Task ResumeConversationAsync(ConversationId conversationId);
    Task CompleteConversationAsync(ConversationId conversationId);
    Task ArchiveConversationAsync(ConversationId conversationId);
    Task RestoreConversationAsync(ConversationId conversationId);
    Task ExpireConversationAsync(ConversationId conversationId);

    // Sessions
    Task StartSessionAsync(ConversationId conversationId, IEnumerable<ParticipantId> participantIds, ConversationMetadata? metadata = null);
    Task EndSessionAsync(ConversationId conversationId, SessionId sessionId, SessionStatus status, string? reason = null);
    Task CloseInactiveSessionsAsync(ConversationId conversationId);

    // Participants
    Task AddParticipantAsync(ConversationId conversationId, UserId userId, ParticipantRole role);
    Task RemoveParticipantAsync(ConversationId conversationId, ParticipantId participantId);

    // Messages & Knowledge
    Task AddMessageAsync(ConversationId conversationId, ParticipantRole role, MessageContent content, UserId senderId, MessageType type, ConversationMetadata? metadata = null);
    Task AttachKnowledgeReferenceAsync(ConversationId conversationId, ConversationAttachment attachment, MessageId messageId);

    // External References
    Task AttachWorkflowExecutionAsync(ConversationId conversationId, ExecutionId executionId);
    Task AttachEvaluationAsync(ConversationId conversationId, EvaluationId evaluationId);
    Task AttachTraceAsync(ConversationId conversationId, TraceId traceId);

    // Memory & Context
    Task UpdateMemoryAsync(ConversationId conversationId, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type, bool isPinned = false, string? semanticReference = null);
    Task UpdateContextAsync(ConversationId conversationId, ConversationContext context);
    Task CreateSnapshotAsync(ConversationId conversationId);
    Task RestoreSnapshotAsync(ConversationId conversationId, SnapshotId snapshotId);

    // Queries
    Task<Conversation> GetConversationAsync(ConversationId conversationId);
    Task<ConversationTimeline> GetTimelineAsync(ConversationId conversationId);
    Task<ConversationStatisticsDto> GetStatisticsAsync(ConversationId conversationId);
}

public record ConversationStatisticsDto(
    int MessageCount,
    int ParticipantCount,
    int SessionCount,
    int WorkflowCount,
    int EvaluationCount,
    int AttachmentCount,
    int TimelineCount,
    TimeSpan TotalDuration,
    double AverageResponseTime
);
