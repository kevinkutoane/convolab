using ConvoLab.Domain.Common;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Domain.Plugins.Events;
using ConvoLab.Domain.Plugins.ValueObjects;

namespace ConvoLab.Domain.Plugins.Aggregates;

public sealed class Plugin : BaseAggregateRoot<PluginId>
{
    private readonly List<string> _capabilities = [];
    private readonly List<string> _permissions = [];

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Publisher { get; private set; } = string.Empty;
    public PluginCategory Category { get; private set; }
    public PluginVersion Version { get; private set; } = PluginVersion.FromString("0.0.0");
    public string ManifestUrl { get; private set; } = string.Empty;
    public string EntryPoint { get; private set; } = string.Empty;
    public string PlatformApiVersion { get; private set; } = "1.0";
    public PluginStatus Status { get; private set; }
    public PluginHealthStatus HealthStatus { get; private set; }
    public string HealthMessage { get; private set; } = "Not checked";
    public DateTimeOffset? LastHealthCheckAt { get; private set; }
    public long Revision { get; private set; }
    public new DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<string> Capabilities => _capabilities;
    public IReadOnlyList<string> Permissions => _permissions;

    private Plugin() : base() { }

    private Plugin(
        PluginId id,
        string key,
        string name,
        string description,
        string publisher,
        PluginCategory category,
        PluginVersion version,
        string manifestUrl,
        string entryPoint,
        string platformApiVersion,
        IEnumerable<string> capabilities,
        IEnumerable<string> permissions,
        PluginStatus status,
        PluginHealthStatus healthStatus,
        string healthMessage,
        DateTimeOffset? lastHealthCheckAt,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) : base(id)
    {
        Key = Required(key, nameof(key)).ToLowerInvariant();
        Name = Required(name, nameof(name));
        Description = description?.Trim() ?? string.Empty;
        Publisher = Required(publisher, nameof(publisher));
        Category = category;
        Version = version;
        ManifestUrl = Required(manifestUrl, nameof(manifestUrl));
        EntryPoint = entryPoint?.Trim() ?? string.Empty;
        PlatformApiVersion = Required(platformApiVersion, nameof(platformApiVersion));
        Replace(_capabilities, capabilities);
        Replace(_permissions, permissions);
        Status = status;
        HealthStatus = healthStatus;
        HealthMessage = healthMessage?.Trim() ?? string.Empty;
        LastHealthCheckAt = lastHealthCheckAt;
        Revision = revision;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Plugin Register(
        string key,
        string name,
        string description,
        string publisher,
        PluginCategory category,
        PluginVersion version,
        string manifestUrl,
        string entryPoint,
        string platformApiVersion,
        IEnumerable<string>? capabilities = null,
        IEnumerable<string>? permissions = null)
    {
        var now = DateTimeOffset.UtcNow;
        var plugin = new Plugin(
            PluginId.CreateUnique(), key, name, description, publisher, category, version,
            manifestUrl, entryPoint, platformApiVersion, capabilities ?? [], permissions ?? [],
            PluginStatus.Installed, PluginHealthStatus.Unknown, "Not checked", null, 1, now, now);
        plugin.AddDomainEvent(new PluginRegisteredEvent(plugin.Id, plugin.Name, plugin.Version));
        return plugin;
    }

    public static Plugin Restore(
        PluginId id,
        string key,
        string name,
        string description,
        string publisher,
        PluginCategory category,
        PluginVersion version,
        string manifestUrl,
        string entryPoint,
        string platformApiVersion,
        IEnumerable<string> capabilities,
        IEnumerable<string> permissions,
        PluginStatus status,
        PluginHealthStatus healthStatus,
        string healthMessage,
        DateTimeOffset? lastHealthCheckAt,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        => new(id, key, name, description, publisher, category, version, manifestUrl, entryPoint,
            platformApiVersion, capabilities, permissions, status, healthStatus, healthMessage,
            lastHealthCheckAt, revision, createdAt, updatedAt);

    public void UpdateMetadata(
        string name,
        string description,
        string publisher,
        PluginCategory category,
        string entryPoint,
        string platformApiVersion,
        IEnumerable<string> capabilities,
        IEnumerable<string> permissions)
    {
        EnsureMutable();
        Name = Required(name, nameof(name));
        Description = description?.Trim() ?? string.Empty;
        Publisher = Required(publisher, nameof(publisher));
        Category = category;
        EntryPoint = entryPoint?.Trim() ?? string.Empty;
        PlatformApiVersion = Required(platformApiVersion, nameof(platformApiVersion));
        Replace(_capabilities, capabilities);
        Replace(_permissions, permissions);
        Touch();
    }

    public void UpdateVersion(PluginVersion version, string manifestUrl)
    {
        EnsureMutable();
        Version = version;
        ManifestUrl = Required(manifestUrl, nameof(manifestUrl));
        Status = PluginStatus.Installed;
        HealthStatus = PluginHealthStatus.Unknown;
        HealthMessage = "Version changed; health check required";
        LastHealthCheckAt = null;
        Touch();
    }

    public void Activate()
    {
        if (Status == PluginStatus.Deprecated)
            throw new InvalidOperationException("A deprecated plugin cannot be activated.");
        if (HealthStatus is PluginHealthStatus.Unknown or PluginHealthStatus.Unhealthy)
            throw new InvalidOperationException("A plugin requires healthy or degraded evidence before activation.");
        Status = PluginStatus.Active;
        Touch();
    }

    public void Deactivate()
    {
        if (Status == PluginStatus.Deprecated)
            throw new InvalidOperationException("A deprecated plugin cannot be changed.");
        Status = PluginStatus.Inactive;
        Touch();
    }

    public void Deprecate()
    {
        Status = PluginStatus.Deprecated;
        Touch();
    }

    public void RecordHealth(PluginHealthStatus status, string message, DateTimeOffset checkedAt)
    {
        HealthStatus = status;
        HealthMessage = message?.Trim() ?? string.Empty;
        LastHealthCheckAt = checkedAt;
        Touch();
    }

    private void EnsureMutable()
    {
        if (Status == PluginStatus.Deprecated)
            throw new InvalidOperationException("A deprecated plugin is immutable.");
        if (Status == PluginStatus.Active)
            throw new InvalidOperationException("Deactivate the plugin before changing its metadata or version.");
    }

    private void Touch()
    {
        Revision++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string Required(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameter} cannot be empty.", parameter);
        return value.Trim();
    }

    private static void Replace(List<string> target, IEnumerable<string> values)
    {
        target.Clear();
        target.AddRange(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }
}
