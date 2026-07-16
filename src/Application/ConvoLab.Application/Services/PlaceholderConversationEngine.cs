using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.Entities;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Application.Services;

public class PlaceholderConversationEngine : IConversationEngine
{
    public Task<ConversationId> CreateConversationAsync(UserId creatorId, string title, ConversationMetadata? metadata = null) => Task.FromResult(ConversationId.CreateUnique());
    public Task StartConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task PauseConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task ResumeConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task CompleteConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task ArchiveConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task RestoreConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task ExpireConversationAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task StartSessionAsync(ConversationId conversationId, IEnumerable<ParticipantId> participantIds, ConversationMetadata? metadata = null) => Task.CompletedTask;
    public Task EndSessionAsync(ConversationId conversationId, SessionId sessionId, SessionStatus status, string? reason = null) => Task.CompletedTask;
    public Task CloseInactiveSessionsAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task AddParticipantAsync(ConversationId conversationId, UserId userId, ParticipantRole role) => Task.CompletedTask;
    public Task RemoveParticipantAsync(ConversationId conversationId, ParticipantId participantId) => Task.CompletedTask;
    public Task AddMessageAsync(ConversationId conversationId, ParticipantRole role, MessageContent content, UserId senderId, MessageType type, ConversationMetadata? metadata = null) => Task.CompletedTask;
    public Task AttachKnowledgeReferenceAsync(ConversationId conversationId, ConversationAttachment attachment, MessageId messageId) => Task.CompletedTask;
    public Task AttachWorkflowExecutionAsync(ConversationId conversationId, ExecutionId executionId) => Task.CompletedTask;
    public Task AttachEvaluationAsync(ConversationId conversationId, EvaluationId evaluationId) => Task.CompletedTask;
    public Task AttachTraceAsync(ConversationId conversationId, TraceId traceId) => Task.CompletedTask;
    public Task UpdateMemoryAsync(ConversationId conversationId, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type, bool isPinned = false, string? semanticReference = null) => Task.CompletedTask;
    public Task UpdateContextAsync(ConversationId conversationId, ConversationContext context) => Task.CompletedTask;
    public Task CreateSnapshotAsync(ConversationId conversationId) => Task.CompletedTask;
    public Task RestoreSnapshotAsync(ConversationId conversationId, SnapshotId snapshotId) => Task.CompletedTask;
    public Task<Conversation> GetConversationAsync(ConversationId conversationId) => Task.FromResult<Conversation>(default!);
    public Task<ConversationTimeline> GetTimelineAsync(ConversationId conversationId) => Task.FromResult<ConversationTimeline>(default!);
    public Task<ConversationStatisticsDto> GetStatisticsAsync(ConversationId conversationId) => Task.FromResult(new ConversationStatisticsDto(0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, 0));
}
