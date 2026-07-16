using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Domain.Knowledge.ValueObjects;

/// <summary>
/// Declares what a connector can do (search, incremental sync, change feeds, etc.).
/// The Knowledge Engine uses capabilities to decide which retrieval strategies a
/// source can serve — connectors are plug-and-play behind this contract.
/// </summary>
public class ConnectorCapabilities : ValueObject
{
    public bool SupportsFullSync { get; private set; }
    public bool SupportsIncrementalSync { get; private set; }
    public bool SupportsChangeFeed { get; private set; }
    public bool SupportsNativeSearch { get; private set; }
    public bool SupportsMetadataExtraction { get; private set; }
    public bool SupportsAccessControlMapping { get; private set; }

    private ConnectorCapabilities() { } // For EF Core

    private ConnectorCapabilities(
        bool fullSync, bool incrementalSync, bool changeFeed,
        bool nativeSearch, bool metadataExtraction, bool aclMapping)
    {
        SupportsFullSync = fullSync;
        SupportsIncrementalSync = incrementalSync;
        SupportsChangeFeed = changeFeed;
        SupportsNativeSearch = nativeSearch;
        SupportsMetadataExtraction = metadataExtraction;
        SupportsAccessControlMapping = aclMapping;
    }

    public static ConnectorCapabilities Create(
        bool supportsFullSync = true,
        bool supportsIncrementalSync = false,
        bool supportsChangeFeed = false,
        bool supportsNativeSearch = false,
        bool supportsMetadataExtraction = false,
        bool supportsAccessControlMapping = false)
    {
        if (!supportsFullSync && !supportsIncrementalSync && !supportsChangeFeed)
            throw new ArgumentException("A connector must support at least one synchronization capability.");
        return new ConnectorCapabilities(
            supportsFullSync, supportsIncrementalSync, supportsChangeFeed,
            supportsNativeSearch, supportsMetadataExtraction, supportsAccessControlMapping);
    }

    public static ConnectorCapabilities Minimal() => Create(supportsFullSync: true);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SupportsFullSync;
        yield return SupportsIncrementalSync;
        yield return SupportsChangeFeed;
        yield return SupportsNativeSearch;
        yield return SupportsMetadataExtraction;
        yield return SupportsAccessControlMapping;
    }
}

/// <summary>
/// An opaque reference to credentials held in a secret store. The domain never
/// stores secrets — only the pointer and the authentication scheme.
/// </summary>
public class AuthenticationReference : ValueObject
{
    public string Scheme { get; private set; }
    public string SecretStoreKey { get; private set; }

    private AuthenticationReference() { Scheme = null!; SecretStoreKey = null!; } // For EF Core

    private AuthenticationReference(string scheme, string secretStoreKey)
    {
        Scheme = scheme;
        SecretStoreKey = secretStoreKey;
    }

    public static AuthenticationReference Create(string scheme, string secretStoreKey)
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("Authentication scheme cannot be empty.", nameof(scheme));
        if (string.IsNullOrWhiteSpace(secretStoreKey))
            throw new ArgumentException("Secret store key cannot be empty.", nameof(secretStoreKey));
        return new AuthenticationReference(scheme, secretStoreKey);
    }

    public static AuthenticationReference None() => new("None", "n/a");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Scheme;
        yield return SecretStoreKey;
    }
}

/// <summary>
/// Governs how and when a connector synchronizes content from its source system.
/// </summary>
public class SynchronizationPolicy : ValueObject
{
    public SynchronizationMode Mode { get; private set; }
    public RefreshSchedule? Schedule { get; private set; }
    public int MaxRetries { get; private set; }
    public bool FailFast { get; private set; }

    private SynchronizationPolicy() { } // For EF Core

    private SynchronizationPolicy(SynchronizationMode mode, RefreshSchedule? schedule, int maxRetries, bool failFast)
    {
        Mode = mode;
        Schedule = schedule;
        MaxRetries = maxRetries;
        FailFast = failFast;
    }

    public static SynchronizationPolicy Create(
        SynchronizationMode mode,
        RefreshSchedule? schedule = null,
        int maxRetries = 3,
        bool failFast = false)
    {
        if (mode == SynchronizationMode.Scheduled && schedule is null)
            throw new ArgumentException("A scheduled synchronization policy requires a refresh schedule.", nameof(schedule));
        if (maxRetries < 0)
            throw new ArgumentException("MaxRetries cannot be negative.", nameof(maxRetries));
        return new SynchronizationPolicy(mode, schedule, maxRetries, failFast);
    }

    public static SynchronizationPolicy Manual() => new(SynchronizationMode.Manual, null, 0, false);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Mode;
        yield return Schedule ?? (object)"none";
        yield return MaxRetries;
        yield return FailFast;
    }
}

/// <summary>
/// Recurring refresh schedule for scheduled synchronization, expressed as an
/// interval plus an optional preferred window start (UTC).
/// </summary>
public class RefreshSchedule : ValueObject
{
    public TimeSpan Interval { get; private set; }
    public TimeSpan? PreferredWindowStartUtc { get; private set; }

    private RefreshSchedule() { } // For EF Core

    private RefreshSchedule(TimeSpan interval, TimeSpan? preferredWindowStartUtc)
    {
        Interval = interval;
        PreferredWindowStartUtc = preferredWindowStartUtc;
    }

    public static RefreshSchedule Every(TimeSpan interval, TimeSpan? preferredWindowStartUtc = null)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Refresh interval must be positive.", nameof(interval));
        return new RefreshSchedule(interval, preferredWindowStartUtc);
    }

    public static RefreshSchedule Daily() => Every(TimeSpan.FromDays(1));
    public static RefreshSchedule Hourly() => Every(TimeSpan.FromHours(1));

    public DateTime NextRunAfter(DateTime lastRunUtc) => lastRunUtc.Add(Interval);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Interval;
        yield return PreferredWindowStartUtc ?? TimeSpan.Zero;
    }
}
