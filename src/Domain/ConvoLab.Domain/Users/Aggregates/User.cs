using ConvoLab.Domain.Common;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Users.Enums;
using ConvoLab.Domain.Users.Events;
namespace ConvoLab.Domain.Users.Aggregates;
public class User : BaseAggregateRoot<UserId> {
    public string Username { get; private set; }
    public string Email { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    private User() : base() { }
    private User(UserId id, string username, string email, UserRole role) : base(id) {
        Username = username; Email = email; Role = role; IsActive = true;
        AddDomainEvent(new UserCreatedEvent(id, username, email));
    }
    public static User Create(string username, string email, UserRole role) => new User(UserId.CreateUnique(), username, email, role);
}
