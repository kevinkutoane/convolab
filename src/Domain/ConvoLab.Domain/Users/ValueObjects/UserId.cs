namespace ConvoLab.Domain.Users.ValueObjects;
public record UserId(Guid Value) {
    public static implicit operator Guid(UserId id) => id.Value;
    public static implicit operator UserId(Guid value) => new(value);
}
