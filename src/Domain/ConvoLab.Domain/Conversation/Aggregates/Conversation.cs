using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Conversation.Entities;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.Events;
namespace ConvoLab.Domain.Conversation.Aggregates;
public class Conversation : BaseAggregateRoot<ConversationId> {
    public string Title { get; private set; }
    public ConversationStatus Status { get; private set; }
    private readonly List<Message> _messages = new();
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();
    private readonly List<Participant> _participants = new();
    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();
    private Conversation() : base() { }
    private Conversation(ConversationId id, string title, Participant initialParticipant) : base(id) {
        Title = title; Status = ConversationStatus.Active; _participants.Add(initialParticipant);
        AddDomainEvent(new ConversationStartedEvent(id, title, initialParticipant.Id));
    }
    public static Conversation StartNew(string title, Participant initialParticipant) => new Conversation(ConversationId.CreateUnique(), title, initialParticipant);
    public void AddMessage(Message message) {
        if (Status == ConversationStatus.Closed) throw new InvalidOperationException("Closed");
        _messages.Add(message);
        AddDomainEvent(new MessageAddedEvent(Id, message.Id, message.SenderId, message.Content.Value));
    }
}
