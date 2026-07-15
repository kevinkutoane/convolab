using ConvoLab.Domain.Common;
using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.Events;
using ConvoLab.Domain.Conversation.Entities;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Conversation.Aggregates;

public class Conversation : BaseAggregateRoot<ConversationId>
{
    public UserId CreatorId { get; private set; }
    public string Title { get; private set; }
    public ConversationStatus Status { get; private set; }
    public ConversationMetadata Metadata { get; private set; }
    public ConversationWindow Window { get; private set; }
    public ConversationContext Context { get; private set; }

    private readonly List<ConversationParticipant> _participants = new();
    public IReadOnlyList<ConversationParticipant> Participants => _participants.AsReadOnly();

    private readonly List<ConversationSession> _sessions = new();
    public IReadOnlyList<ConversationSession> Sessions => _sessions.AsReadOnly();

    private readonly List<ConversationMessage> _messages = new();
    public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();

    private readonly List<ConversationAttachment> _attachments = new();
    public IReadOnlyList<ConversationAttachment> Attachments => _attachments.AsReadOnly();

    private readonly List<ConversationMemory> _memories = new();
    public IReadOnlyList<ConversationMemory> Memories => _memories.AsReadOnly();

    private readonly List<ConversationSnapshot> _snapshots = new();
    public IReadOnlyList<ConversationSnapshot> Snapshots => _snapshots.AsReadOnly();

    private readonly List<ExecutionId> _workflowExecutionIds = new();
    public IReadOnlyList<ExecutionId> WorkflowExecutionIds => _workflowExecutionIds.AsReadOnly();

    private readonly List<EvaluationId> _evaluationIds = new();
    public IReadOnlyList<EvaluationId> EvaluationIds => _evaluationIds.AsReadOnly();

    private readonly List<TraceId> _traceIds = new();
    public IReadOnlyList<TraceId> TraceIds => _traceIds.AsReadOnly();

    public ConversationTimeline Timeline { get; private set; }

    private Conversation(ConversationId id, UserId creatorId, string title, ConversationMetadata metadata, ConversationWindow window, ConversationContext context)
        : base(id)
    {
        CreatorId = creatorId;
        Title = title;
        Status = ConversationStatus.Created;
        Metadata = metadata;
        Window = window;
        Context = context;
        Timeline = ConversationTimeline.Create(new List<TimelineEntry>());

        AddDomainEvent(new ConversationCreatedEvent(id, creatorId, DateTime.UtcNow));
        AddTimelineEntry("Conversation Created", $"Conversation '{title}' created by {creatorId.Value}");
    }

    public static Conversation Create(UserId creatorId, string title, ConversationMetadata metadata, ConversationWindow window, ConversationContext context)
    {
        return new(ConversationId.CreateUnique(), creatorId, title, metadata, window, context);
    }

    #region Lifecycle Management

    public void Start()
    {
        EnsureStatusTransition(ConversationStatus.Started);
        Status = ConversationStatus.Started;
        AddDomainEvent(new ConversationStartedEvent(Id, DateTime.UtcNow));
        AddTimelineEntry("Conversation Started", "Conversation has been started.");
    }

    public void Activate()
    {
        EnsureStatusTransition(ConversationStatus.Active);
        Status = ConversationStatus.Active;
        AddTimelineEntry("Conversation Active", "Conversation is now active.");
    }

    public void Wait()
    {
        EnsureStatusTransition(ConversationStatus.Waiting);
        Status = ConversationStatus.Waiting;
        AddTimelineEntry("Conversation Waiting", "Conversation is waiting for input or action.");
    }

    public void Process()
    {
        EnsureStatusTransition(ConversationStatus.Processing);
        Status = ConversationStatus.Processing;
        AddTimelineEntry("Conversation Processing", "Conversation is currently processing.");
    }

    public void Complete()
    {
        EnsureStatusTransition(ConversationStatus.Completed);
        Status = ConversationStatus.Completed;
        AddDomainEvent(new ConversationCompletedEvent(Id, DateTime.UtcNow));
        AddTimelineEntry("Conversation Completed", "Conversation has been completed.");
    }

    public void Archive()
    {
        EnsureStatusTransition(ConversationStatus.Archived);
        Status = ConversationStatus.Archived;
        AddDomainEvent(new ConversationArchivedEvent(Id, DateTime.UtcNow));
        AddTimelineEntry("Conversation Archived", "Conversation has been archived.");
    }

    public void SoftDelete()
    {
        EnsureStatusTransition(ConversationStatus.SoftDeleted);
        Status = ConversationStatus.SoftDeleted;
        AddTimelineEntry("Conversation Deleted", "Conversation has been soft deleted.");
    }

    private void EnsureStatusTransition(ConversationStatus newStatus)
    {
        bool isValid = Status switch
        {
            ConversationStatus.Created => newStatus == ConversationStatus.Started || newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.Started => newStatus == ConversationStatus.Active || newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.Active => newStatus == ConversationStatus.Waiting || newStatus == ConversationStatus.Processing || newStatus == ConversationStatus.Completed || newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.Waiting => newStatus == ConversationStatus.Active || newStatus == ConversationStatus.Processing || newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.Processing => newStatus == ConversationStatus.Active || newStatus == ConversationStatus.Waiting || newStatus == ConversationStatus.Completed || newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.Completed => newStatus == ConversationStatus.Archived || newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.Archived => newStatus == ConversationStatus.SoftDeleted,
            ConversationStatus.SoftDeleted => false,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid transition from {Status} to {newStatus}");
        }
    }

    #endregion

    #region Session Management

    public void StartSession(IEnumerable<ParticipantId> participantIds, ConversationMetadata? metadata = null)
    {
        var session = ConversationSession.Create(participantIds, metadata);
        _sessions.Add(session);
        AddDomainEvent(new SessionStartedEvent(Id, session.Id, DateTime.UtcNow));
        AddTimelineEntry("Session Started", $"New session {session.Id.Value} started.");
    }

    public void EndSession(SessionId sessionId, SessionStatus status, string? reason = null)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session == null) throw new InvalidOperationException("Session not found.");
        
        session.EndSession(status, reason);
        AddDomainEvent(new SessionEndedEvent(Id, sessionId, DateTime.UtcNow, reason));
        AddTimelineEntry("Session Ended", $"Session {sessionId.Value} ended. Reason: {reason ?? "None"}");
    }

    #endregion

    #region Participant Management

    public void AddParticipant(UserId userId, ParticipantRole role)
    {
        if (_participants.Any(p => p.UserId == userId))
        {
            throw new InvalidOperationException("User is already a participant.");
        }
        
        var participant = ConversationParticipant.Create(userId, role);
        _participants.Add(participant);
        AddDomainEvent(new ParticipantJoinedEvent(Id, participant.Id, userId, DateTime.UtcNow));
        AddTimelineEntry("Participant Joined", $"User {userId.Value} joined as {role}.");
    }

    public void RemoveParticipant(ParticipantId participantId)
    {
        var participant = _participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null) throw new InvalidOperationException("Participant not found.");
        
        participant.LeaveConversation();
        AddDomainEvent(new ParticipantRemovedEvent(Id, participantId, participant.UserId, DateTime.UtcNow));
        AddTimelineEntry("Participant Removed", $"Participant {participantId.Value} left the conversation.");
    }

    #endregion

    #region Message Management

    public void AddMessage(ConversationMessage message)
    {
        if (Status == ConversationStatus.Completed || Status == ConversationStatus.Archived || Status == ConversationStatus.SoftDeleted)
        {
            throw new InvalidOperationException("Cannot add messages to a closed conversation.");
        }

        _messages.Add(message);
        
        // Auto-link to current session if active
        var currentSession = _sessions.LastOrDefault(s => s.Status == SessionStatus.Active);
        currentSession?.AddMessage(message.Id);

        AddDomainEvent(new MessageAddedEvent(Id, message.Id, message.SenderId, DateTime.UtcNow));
        AddTimelineEntry("Message Added", $"Message {message.Id.Value} added by {message.Role}.");
    }

    public void AddAttachment(ConversationAttachment attachment, MessageId messageId)
    {
        if (!_messages.Any(m => m.Id == messageId)) throw new InvalidOperationException("Message not found.");
        
        _attachments.Add(attachment);
        AddDomainEvent(new AttachmentAddedEvent(Id, attachment.Id, messageId, DateTime.UtcNow));
        AddTimelineEntry("Attachment Added", $"Attachment {attachment.Id.Value} added to message {messageId.Value}.");
    }

    #endregion

    #region Memory & Context

    public void UpdateMemory(ConversationMemory memory)
    {
        var existing = _memories.FirstOrDefault(m => m.Type == memory.Type);
        if (existing != null) _memories.Remove(existing);
        
        _memories.Add(memory);
        AddDomainEvent(new MemoryUpdatedEvent(Id, memory.Id, DateTime.UtcNow));
        AddTimelineEntry("Memory Updated", $"Memory of type {memory.Type} updated.");
    }

    public void TakeSnapshot()
    {
        var snapshot = ConversationSnapshot.Create(Id, "Manual Snapshot");
        _snapshots.Add(snapshot);
        AddDomainEvent(new SnapshotCreatedEvent(Id, snapshot.Id, DateTime.UtcNow));
        AddTimelineEntry("Snapshot Created", $"Conversation snapshot {snapshot.Id.Value} taken.");
    }

    public void UpdateContext(ConversationContext newContext)
    {
        Context = newContext;
        AddTimelineEntry("Context Updated", "Conversation context has been updated.");
    }

    #endregion

    #region External References

    public void LinkWorkflow(ExecutionId executionId)
    {
        if (!_workflowExecutionIds.Contains(executionId))
        {
            _workflowExecutionIds.Add(executionId);
            AddDomainEvent(new WorkflowLinkedEvent(Id, executionId, DateTime.UtcNow));
            AddTimelineEntry("Workflow Linked", $"Workflow execution {executionId.Value} linked.");
        }
    }

    public void LinkEvaluation(EvaluationId evaluationId)
    {
        if (!_evaluationIds.Contains(evaluationId))
        {
            _evaluationIds.Add(evaluationId);
            AddDomainEvent(new EvaluationLinkedEvent(Id, evaluationId, DateTime.UtcNow));
            AddTimelineEntry("Evaluation Linked", $"Evaluation {evaluationId.Value} linked.");
        }
    }

    public void LinkTrace(TraceId traceId)
    {
        if (!_traceIds.Contains(traceId))
        {
            _traceIds.Add(traceId);
            AddDomainEvent(new TraceLinkedEvent(Id, traceId, DateTime.UtcNow));
            AddTimelineEntry("Trace Linked", $"Trace {traceId.Value} linked.");
        }
    }

    #endregion

    private void AddTimelineEntry(string eventName, string description)
    {
        var entry = TimelineEntry.Create(eventName, description, Metadata);
        Timeline = Timeline.AddEntry(entry);
    }

    private Conversation() { 
        Title = null!;
        Metadata = null!;
        Window = null!;
        Context = null!;
        Timeline = null!;
        CreatorId = null!;
    }
}
