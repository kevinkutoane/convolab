using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Aggregates;

/// <summary>
/// The KnowledgeConnector aggregate root. The plug-and-play bridge between a
/// knowledge source and its origin system (SharePoint, Confluence, SQL, APIs...).
/// The domain models capabilities, health, authentication references, and
/// synchronization policy — never the integration itself.
///
/// Core invariants:
/// - A connector serves exactly one knowledge source.
/// - Credentials are never stored; only an AuthenticationReference.
/// - A connector must be validated before it can synchronize.
/// - Repeated failures degrade the connector automatically.
/// </summary>
public class KnowledgeConnector : BaseAggregateRoot<KnowledgeConnectorId>
{
    private const int ConsecutiveFailuresBeforeDegraded = 3;

    public string Name { get; private set; }
    public KnowledgeSourceId SourceId { get; private set; }
    public KnowledgeSourceType SourceType { get; private set; }
    public ConnectorStatus Status { get; private set; }
    public ConnectorCapabilities Capabilities { get; private set; }
    public AuthenticationReference Authentication { get; private set; }
    public SynchronizationPolicy SyncPolicy { get; private set; }
    public KnowledgeHealth Health { get; private set; }
    public DateTime? LastSynchronizedAt { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private KnowledgeConnector()
    {
        Name = null!; SourceId = null!; Capabilities = null!;
        Authentication = null!; SyncPolicy = null!; Health = null!;
    } // For EF Core

    private KnowledgeConnector(
        KnowledgeConnectorId id,
        string name,
        KnowledgeSourceId sourceId,
        KnowledgeSourceType sourceType,
        ConnectorCapabilities capabilities,
        AuthenticationReference authentication,
        SynchronizationPolicy syncPolicy) : base(id)
    {
        Name = name;
        SourceId = sourceId;
        SourceType = sourceType;
        Capabilities = capabilities;
        Authentication = authentication;
        SyncPolicy = syncPolicy;
        Status = ConnectorStatus.Registered;
        Health = KnowledgeHealth.Unknown();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ConnectorRegisteredEvent(id, sourceId, sourceType));
    }

    public static KnowledgeConnector Register(
        string name,
        KnowledgeSourceId sourceId,
        KnowledgeSourceType sourceType,
        ConnectorCapabilities? capabilities = null,
        AuthenticationReference? authentication = null,
        SynchronizationPolicy? syncPolicy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connector name cannot be empty.", nameof(name));

        return new KnowledgeConnector(
            KnowledgeConnectorId.CreateUnique(),
            name,
            sourceId ?? throw new ArgumentNullException(nameof(sourceId)),
            sourceType,
            capabilities ?? ConnectorCapabilities.Minimal(),
            authentication ?? AuthenticationReference.None(),
            syncPolicy ?? SynchronizationPolicy.Manual());
    }

    #region Validation & Lifecycle

    /// <summary>Begins connector validation (reachability, permissions, schema checks).</summary>
    public void BeginValidation()
    {
        if (Status is ConnectorStatus.Disabled)
            throw new InvalidOperationException("Cannot validate a disabled connector.");
        Status = ConnectorStatus.Validating;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Marks validation as successful, activating the connector.</summary>
    public void CompleteValidation()
    {
        if (Status != ConnectorStatus.Validating)
            throw new InvalidOperationException("Connector is not being validated.");
        Status = ConnectorStatus.Active;
        Health = KnowledgeHealth.Healthy("Validation succeeded.");
        ConsecutiveFailures = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Marks validation as failed.</summary>
    public void FailValidation(string reason)
    {
        if (Status != ConnectorStatus.Validating)
            throw new InvalidOperationException("Connector is not being validated.");
        Status = ConnectorStatus.Failed;
        Health = KnowledgeHealth.Unhealthy(reason);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ConnectorFailedEvent(Id, SourceId, reason));
    }

    public void Disable()
    {
        Status = ConnectorStatus.Disabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        if (Status != ConnectorStatus.Disabled)
            throw new InvalidOperationException("Only a disabled connector can be enabled.");
        Status = ConnectorStatus.Registered;
        Health = KnowledgeHealth.Unknown();
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Synchronization

    /// <summary>Begins a synchronization run. Only validated (Active/Degraded) connectors may sync.</summary>
    public void BeginSynchronization()
    {
        if (Status is not (ConnectorStatus.Active or ConnectorStatus.Degraded))
            throw new InvalidOperationException($"Connector must be Active or Degraded to synchronize. Current status: {Status}.");
        Status = ConnectorStatus.Syncing;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Records a successful synchronization run and restores health.</summary>
    public void CompleteSynchronization(int documentsSynchronized, TimeSpan duration)
    {
        if (Status != ConnectorStatus.Syncing)
            throw new InvalidOperationException("Connector is not synchronizing.");
        if (documentsSynchronized < 0)
            throw new ArgumentException("Documents synchronized cannot be negative.", nameof(documentsSynchronized));

        Status = ConnectorStatus.Active;
        Health = KnowledgeHealth.Healthy($"Synchronized {documentsSynchronized} documents in {duration.TotalSeconds:F1}s.");
        LastSynchronizedAt = DateTime.UtcNow;
        ConsecutiveFailures = 0;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ConnectorSynchronizedEvent(Id, SourceId, documentsSynchronized, duration));
    }

    /// <summary>
    /// Records a failed synchronization run. Repeated failures degrade the
    /// connector; exceeding the retry policy fails it outright.
    /// </summary>
    public void FailSynchronization(string reason)
    {
        if (Status != ConnectorStatus.Syncing)
            throw new InvalidOperationException("Connector is not synchronizing.");

        ConsecutiveFailures++;

        if (SyncPolicy.FailFast || ConsecutiveFailures > SyncPolicy.MaxRetries)
        {
            Status = ConnectorStatus.Failed;
            Health = KnowledgeHealth.Unhealthy(reason);
        }
        else if (ConsecutiveFailures >= ConsecutiveFailuresBeforeDegraded)
        {
            Status = ConnectorStatus.Degraded;
            Health = KnowledgeHealth.Degraded(reason);
        }
        else
        {
            Status = ConnectorStatus.Active;
            Health = KnowledgeHealth.Degraded(reason);
        }

        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ConnectorFailedEvent(Id, SourceId, reason));
    }

    /// <summary>Computes the next scheduled run, when the policy defines a schedule.</summary>
    public DateTime? NextScheduledRun()
    {
        if (SyncPolicy.Mode != SynchronizationMode.Scheduled || SyncPolicy.Schedule is null)
            return null;
        var reference = LastSynchronizedAt ?? CreatedAt;
        return SyncPolicy.Schedule.NextRunAfter(reference);
    }

    #endregion

    #region Configuration

    public void UpdateCapabilities(ConnectorCapabilities capabilities)
    {
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateAuthentication(AuthenticationReference authentication)
    {
        Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSyncPolicy(SynchronizationPolicy syncPolicy)
    {
        SyncPolicy = syncPolicy ?? throw new ArgumentNullException(nameof(syncPolicy));
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion
}
