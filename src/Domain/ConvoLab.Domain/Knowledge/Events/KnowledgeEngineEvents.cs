using ConvoLab.Domain.Events;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Events;

/// <summary>Raised when a new enterprise knowledge source is registered with the platform.</summary>
public record KnowledgeSourceRegisteredEvent(
    KnowledgeSourceId SourceId,
    string Name,
    KnowledgeSourceType SourceType,
    Guid OwnerId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a document has been ingested and chunked (indexed) into a source.</summary>
public record KnowledgeIndexedEvent(
    KnowledgeSourceId SourceId,
    KnowledgeDocumentId DocumentId,
    int ChunkCount) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when knowledge content changes and a new version is created.</summary>
public record KnowledgeUpdatedEvent(
    KnowledgeSourceId SourceId,
    KnowledgeDocumentId DocumentId,
    string NewVersion) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a document is deprecated and should no longer be retrieved.</summary>
public record KnowledgeDeprecatedEvent(
    KnowledgeSourceId SourceId,
    KnowledgeDocumentId DocumentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a document version is published and becomes retrievable.</summary>
public record KnowledgeVersionPublishedEvent(
    KnowledgeSourceId SourceId,
    KnowledgeDocumentId DocumentId,
    string Version) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a document is archived and removed from the retrievable estate.</summary>
public record KnowledgeArchivedEvent(
    KnowledgeSourceId SourceId,
    KnowledgeDocumentId DocumentId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a retrieval query has been executed against the knowledge estate.</summary>
public record KnowledgeRetrievedEvent(
    KnowledgeQueryId QueryId,
    RetrievalStrategyType Strategy,
    int ResultCount,
    Guid? ConversationId,
    Guid? WorkflowId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a knowledge package has been assembled for the Prompt Engine.</summary>
public record KnowledgePackageCreatedEvent(
    KnowledgePackageId PackageId,
    KnowledgeQueryId QueryId,
    int ResultCount,
    int TotalEstimatedTokens) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a snapshot of a collection's published knowledge is captured.</summary>
public record KnowledgeSnapshotCreatedEvent(
    KnowledgeSnapshotId SnapshotId,
    KnowledgeCollectionId CollectionId,
    string Label,
    int DocumentCount) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a new connector is registered for a knowledge source.</summary>
public record ConnectorRegisteredEvent(
    KnowledgeConnectorId ConnectorId,
    KnowledgeSourceId SourceId,
    KnowledgeSourceType SourceType) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a connector completes a synchronization run.</summary>
public record ConnectorSynchronizedEvent(
    KnowledgeConnectorId ConnectorId,
    KnowledgeSourceId SourceId,
    int DocumentsSynchronized,
    TimeSpan Duration) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a connector synchronization run fails.</summary>
public record ConnectorFailedEvent(
    KnowledgeConnectorId ConnectorId,
    KnowledgeSourceId SourceId,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Raised when a knowledge collection is created.</summary>
public record KnowledgeCollectionCreatedEvent(
    KnowledgeCollectionId CollectionId,
    string Name,
    Guid OwnerId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
