using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

/// <summary>
/// Represents the ownership of a prompt, including the team and individual responsible.
/// Ownership is required for governance and approval workflows.
/// </summary>
public class PromptOwner : ValueObject
{
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; }
    public string? Team { get; private set; }

    private PromptOwner(Guid userId, string displayName, string? team)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("PromptOwner UserId cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("PromptOwner DisplayName cannot be empty.", nameof(displayName));

        UserId = userId;
        DisplayName = displayName;
        Team = team;
    }

    public static PromptOwner Create(Guid userId, string displayName, string? team = null)
        => new(userId, displayName, team);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return UserId;
    }

    private PromptOwner() { DisplayName = null!; }
}
