using ConvoLab.Domain.Events;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.ValueObjects;

namespace ConvoLab.Domain.Policy.Events;

/// <summary>Raised when a policy definition is created.</summary>
public record PolicyCreatedEvent(PolicyDefinitionId PolicyId, string Name, PolicyDomain Domain) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a policy is activated and begins governing.</summary>
public record PolicyActivatedEvent(PolicyDefinitionId PolicyId, string Name) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised on every policy evaluation, for audit and compliance.</summary>
public record PolicyEvaluatedEvent(PolicyDefinitionId PolicyId, PolicyDomain Domain, PolicyEffect Effect) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
