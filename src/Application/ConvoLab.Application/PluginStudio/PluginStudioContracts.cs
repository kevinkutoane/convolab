using ConvoLab.Domain.Plugins.Enums;

namespace ConvoLab.Application.PluginStudio;

public sealed record PluginMetricsDto(
    int Registered,
    int Active,
    int Healthy,
    int Unhealthy,
    int Categories,
    int HealthChecks);

public sealed record PluginSummaryDto(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Publisher,
    string Version,
    PluginCategory Category,
    PluginStatus Status,
    PluginHealthStatus HealthStatus,
    string HealthMessage,
    string ManifestUrl,
    string PlatformApiVersion,
    bool Compatible,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset? LastHealthCheckAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record PluginHealthCheckDto(
    Guid Id,
    Guid PluginId,
    PluginHealthStatus Status,
    string Message,
    int DurationMs,
    string Source,
    DateTimeOffset CheckedAt);

public sealed record PluginDetailDto(
    PluginSummaryDto Summary,
    string EntryPoint,
    IReadOnlyList<string> Permissions,
    string ConfigurationSchema,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<PluginHealthCheckDto> HealthHistory,
    IReadOnlyList<PluginSummaryDto> VersionHistory);

public sealed record PluginOverviewDto(
    PluginMetricsDto Metrics,
    IReadOnlyList<PluginSummaryDto> Plugins,
    IReadOnlyList<PluginHealthCheckDto> RecentHealthChecks,
    IReadOnlyList<string> Categories,
    DateTimeOffset GeneratedAt);

public sealed record RegisterPluginCommand(
    string Key,
    string Name,
    string Description,
    string Publisher,
    string Version,
    PluginCategory Category,
    string ManifestUrl,
    string EntryPoint,
    string PlatformApiVersion,
    IReadOnlyList<string>? Capabilities = null,
    IReadOnlyList<string>? Permissions = null,
    string ConfigurationSchema = "{}",
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record UpdatePluginCommand(
    string Name,
    string Description,
    string Publisher,
    PluginCategory Category,
    string EntryPoint,
    string PlatformApiVersion,
    IReadOnlyList<string>? Capabilities,
    IReadOnlyList<string>? Permissions,
    string ConfigurationSchema,
    IReadOnlyDictionary<string, string>? Metadata,
    long Revision);

public sealed record UpdatePluginVersionCommand(
    string Version,
    string ManifestUrl,
    long Revision);

public sealed record PluginState(
    Guid Id,
    Guid PluginKey,
    string Key,
    string Name,
    string Description,
    string Publisher,
    string Version,
    PluginCategory Category,
    PluginStatus Status,
    PluginHealthStatus HealthStatus,
    string HealthMessage,
    string ManifestUrl,
    string EntryPoint,
    string PlatformApiVersion,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions,
    string ConfigurationSchema,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset? LastHealthCheckAt,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PluginUpdateState(
    PluginState Plugin,
    long ExpectedRevision);

public sealed record PluginHealthCheckState(
    Guid Id,
    Guid PluginId,
    PluginHealthStatus Status,
    string Message,
    int DurationMs,
    string Source,
    DateTimeOffset CheckedAt);

public sealed record PluginProbeRequest(
    string Key,
    string ManifestUrl,
    string EntryPoint,
    PluginCategory Category);

public sealed record PluginProbeResult(
    PluginHealthStatus Status,
    string Message,
    int DurationMs,
    string Source);

public interface IPluginHealthProbe
{
    Task<PluginProbeResult> ProbeAsync(PluginProbeRequest request, CancellationToken cancellationToken = default);
}

public interface IPluginStudioRepository
{
    Task<int> CountPluginsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PluginState>> ListPluginsAsync(CancellationToken cancellationToken = default);
    Task<PluginState?> GetPluginAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PluginState?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PluginState>> GetVersionHistoryAsync(Guid pluginKey, CancellationToken cancellationToken = default);
    Task AddPluginAsync(PluginState plugin, CancellationToken cancellationToken = default);
    Task UpdatePluginAsync(PluginState plugin, long expectedRevision, CancellationToken cancellationToken = default);
    Task UpdatePluginsAsync(IReadOnlyList<PluginUpdateState> updates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PluginHealthCheckState>> ListHealthChecksAsync(int limit = 100, Guid? pluginId = null, CancellationToken cancellationToken = default);
    Task AddHealthCheckAsync(PluginHealthCheckState healthCheck, CancellationToken cancellationToken = default);
    Task RecordHealthCheckAsync(PluginUpdateState update, PluginHealthCheckState healthCheck, CancellationToken cancellationToken = default);
}

public interface IPluginStudioService
{
    Task<PluginOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PluginSummaryDto>> ListPluginsAsync(CancellationToken cancellationToken = default);
    Task<PluginDetailDto> GetPluginAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PluginDetailDto> RegisterAsync(RegisterPluginCommand command, CancellationToken cancellationToken = default);
    Task<PluginDetailDto> UpdateAsync(Guid id, UpdatePluginCommand command, CancellationToken cancellationToken = default);
    Task<PluginDetailDto> UpdateVersionAsync(Guid id, UpdatePluginVersionCommand command, CancellationToken cancellationToken = default);
    Task<PluginDetailDto> TransitionAsync(Guid id, string action, CancellationToken cancellationToken = default);
    Task<PluginHealthCheckDto> CheckHealthAsync(Guid id, CancellationToken cancellationToken = default);
}

