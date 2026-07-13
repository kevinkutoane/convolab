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
    }

    public static Conversation Create(UserId creatorId, string title, ConversationMetadata metadata, ConversationWindow window, ConversationContext context)
    {
        return new(ConversationId.CreateUnique(), creatorId, title, metadata, window, context);
    }

    public void Start()
    {
        if (Status != ConversationStatus.Created)
        {
            throw new InvalidOperationException("Conversation can only be started from 'Created' status.");
        }
        Status = ConversationStatus.Started;
        AddDomainEvent(new ConversationStartedEvent(Id, DateTime.UtcNow));
    }

    public void End(string? reason = null)
    {
        if (Status == ConversationStatus.Completed || Status == ConversationStatus.Archived || Status == ConversationStatus.Deleted)
        {
            throw new InvalidOperationException("Conversation is already ended, archived, or deleted.");
        }
        Status = ConversationStatus.Completed;
        AddDomainEvent(new ConversationEndedEvent(Id, DateTime.UtcNow, reason));
    }

    public void Archive()
    {
        if (Status == ConversationStatus.Archived || Status == ConversationStatus.Deleted)
        {
            throw new InvalidOperationException("Conversation is already archived or deleted.");
        }
        Status = ConversationStatus.Archived;
        AddDomainEvent(new ConversationArchivedEvent(Id, DateTime.UtcNow));
    }

    public void AddParticipant(ConversationParticipant participant)
    {
        if (_participants.Any(p => p.Id == participant.Id))
        {
            throw new InvalidOperationException("Participant already exists in this conversation.");
        }
        _participants.Add(participant);
        AddDomainEvent(new ParticipantJoinedEvent(Id, participant.Id, participant.UserId, DateTime.UtcNow));
    }

    public void RemoveParticipant(ParticipantId participantId)
    {
        var participant = _participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null)
        {
            throw new InvalidOperationException("Participant not found in this conversation.");
        }
        _participants.Remove(participant);
        AddDomainEvent(new ParticipantLeftEvent(Id, participant.Id, participant.UserId, DateTime.UtcNow));
    }

    public void AddMessage(ConversationMessage message)
    {
        if (Status != ConversationStatus.Active && Status != ConversationStatus.Started && Status != ConversationStatus.Waiting && Status != ConversationStatus.Processing)
        {
            throw new InvalidOperationException("Messages can only be added to active, started, waiting or processing conversations.");
        }
        _messages.Add(message);
        AddDomainEvent(new MessageAddedEvent(Id, message.Id, message.SenderId, DateTime.UtcNow));
    }

    public void AddAttachment(ConversationAttachment attachment, MessageId messageId)
    {
        if (!_messages.Any(m => m.Id == messageId))
        {
            throw new InvalidOperationException("Message not found to attach the file to.");
        }
        _attachments.Add(attachment);
        AddDomainEvent(new AttachmentAddedEvent(Id, attachment.Id, messageId, DateTime.UtcNow));
    }

    public void UpdateMemory(ConversationMemory memory)
    {
        var existingMemory = _memories.FirstOrDefault(m => m.Id == memory.Id);
        if (existingMemory != null)
        {
            _memories.Remove(existingMemory);
        }
        _memories.Add(memory);
        AddDomainEvent(new MemoryUpdatedEvent(Id, memory.Id, DateTime.UtcNow));
    }

    public void AddSession(ConversationSession session)
    {
        _sessions.Add(session);
        AddDomainEvent(new SessionStartedEvent(Id, session.Id, DateTime.UtcNow));
    }

    public void EndSession(SessionId sessionId, SessionStatus status, string? reason = null)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session == null)
        {
            throw new InvalidOperationException("Session not found.");
        }
        session.EndSession(status, reason);
        AddDomainEvent(new SessionEndedEvent(Id, sessionId, DateTime.UtcNow, reason));
    }

    public void AttachWorkflowExecution(ExecutionId executionId)
    {
        if (_workflowExecutionIds.Contains(executionId))
        {
            throw new InvalidOperationException("Workflow execution already attached.");
        }
        _workflowExecutionIds.Add(executionId);
        AddDomainEvent(new WorkflowAttachedEvent(Id, executionId, DateTime.UtcNow));
    }

    public void AttachEvaluation(EvaluationId evaluationId)
    {
        if (_evaluationIds.Contains(evaluationId))
        {
            throw new InvalidOperationException("Evaluation already attached.");
        }
        _evaluationIds.Add(evaluationId);
        AddDomainEvent(new EvaluationAttachedEvent(Id, evaluationId, DateTime.UtcNow));
    }

    public void AttachTrace(TraceId traceId)
    {
        if (_traceIds.Contains(traceId))
        {
            throw new InvalidOperationException("Trace already attached.");
        }
        _traceIds.Add(traceId);
        AddDomainEvent(new TraceAttachedEvent(Id, traceId, DateTime.UtcNow));
    }

    public void AddTimelineEntry(TimelineEntry entry)
    {
        Timeline = Timeline.AddEntry(entry);
    }

    public void UpdateContext(ConversationContext newContext)
    {
        Context = newContext;
    }

    public void AddSnapshot(ConversationSnapshot snapshot)
    {
        _snapshots.Add(snapshot);
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
