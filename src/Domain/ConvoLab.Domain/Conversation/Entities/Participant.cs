using ConvoLab.Domain.Common;
using ConvoLab.Domain.Users.ValueObjects;
namespace ConvoLab.Domain.Conversation.Entities;
public class Participant : BaseEntity<UserId> {
    public string Role { get; private set; }
    private Participant() { }
    private Participant(UserId id, string role) : base(id) { Role = role; }
    public static Participant Create(UserId userId, string role) => new Participant(userId, role);
}
