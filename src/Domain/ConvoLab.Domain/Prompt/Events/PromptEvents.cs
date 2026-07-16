using ConvoLab.Domain.Events;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Events;

/// <summary>Raised when a new Prompt aggregate is created.</summary>
public record PromptCreatedEvent(
    PromptId PromptId,
    string Name,
    string Category,
    Guid AuthorId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a new immutable version is created for an existing prompt.</summary>
public record PromptVersionCreatedEvent(
    PromptId PromptId,
    PromptVersionId VersionId,
    string SemanticVersion,
    Guid AuthorId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt version passes the governance approval process.</summary>
public record PromptApprovedEvent(
    PromptId PromptId,
    PromptVersionId VersionId,
    Guid ReviewerId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt version is rejected during the approval process.</summary>
public record PromptRejectedEvent(
    PromptId PromptId,
    PromptVersionId VersionId,
    Guid ReviewerId,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt is successfully rendered with variables injected.</summary>
public record PromptRenderedEvent(
    PromptId PromptId,
    PromptVersionId VersionId,
    DateTime RenderedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt is deprecated (superseded but still accessible).</summary>
public record PromptDeprecatedEvent(
    PromptId PromptId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt is archived and removed from active use.</summary>
public record PromptArchivedEvent(
    PromptId PromptId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when an archived prompt is restored to Draft status.</summary>
public record PromptRestoredEvent(
    PromptId PromptId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt experiment begins running.</summary>
public record ExperimentStartedEvent(
    PromptExperimentId ExperimentId,
    string Name,
    Guid CreatedByUserId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a prompt experiment concludes and results are available.</summary>
public record ExperimentCompletedEvent(
    PromptExperimentId ExperimentId,
    string Name) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
