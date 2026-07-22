namespace ConvoLab.Infrastructure.PluginStudio;

public sealed class PluginRecord
{
    public Guid Id { get; set; }
    public Guid PluginKey { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = string.Empty;
    public string HealthMessage { get; set; } = string.Empty;
    public string ManifestUrl { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = string.Empty;
    public string PlatformApiVersion { get; set; } = string.Empty;
    public string CapabilitiesJson { get; set; } = "[]";
    public string PermissionsJson { get; set; } = "[]";
    public string ConfigurationSchema { get; set; } = "{}";
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset? LastHealthCheckAt { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PluginHealthCheckRecord
{
    public Guid Id { get; set; }
    public Guid PluginId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; }
}

