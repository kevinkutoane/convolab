using System.Text.Json;
using System.Text.RegularExpressions;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Plugins.Aggregates;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Domain.Plugins.ValueObjects;

namespace ConvoLab.Application.PluginStudio;

public sealed partial class PluginStudioService(
    IPluginStudioRepository repository,
    IPluginHealthProbe healthProbe) : IPluginStudioService, IPluginManager
{
    private const string CurrentPlatformApiVersion = "1.0";
    private static readonly SemaphoreSlim SeedGate = new(1, 1);

    public async Task<PluginOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var plugins = await repository.ListPluginsAsync(cancellationToken);
        var checks = await repository.ListHealthChecksAsync(120, cancellationToken: cancellationToken);
        var summaries = plugins.Select(MapSummary).OrderByDescending(item => item.UpdatedAt).ToList();
        var latestLogicalPlugins = plugins
            .GroupBy(item => item.PluginKey)
            .Select(group => group.OrderByDescending(item => item.CreatedAt).First())
            .ToList();
        var metrics = new PluginMetricsDto(
            latestLogicalPlugins.Count,
            plugins.Count(item => item.Status == PluginStatus.Active),
            latestLogicalPlugins.Count(item => item.HealthStatus == PluginHealthStatus.Healthy),
            latestLogicalPlugins.Count(item => item.HealthStatus == PluginHealthStatus.Unhealthy),
            latestLogicalPlugins.Select(item => item.Category).Distinct().Count(),
            checks.Count);
        return new PluginOverviewDto(
            metrics,
            summaries,
            checks.Take(30).Select(MapHealth).ToList(),
            Enum.GetNames<PluginCategory>(),
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PluginSummaryDto>> ListPluginsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return (await repository.ListPluginsAsync(cancellationToken))
            .Select(MapSummary)
            .OrderBy(item => item.Name)
            .ThenByDescending(item => item.UpdatedAt)
            .ToList();
    }

    public async Task<PluginDetailDto> GetPluginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var state = await repository.GetPluginAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{id}' was not found.");
        var checks = await repository.ListHealthChecksAsync(80, id, cancellationToken);
        var versions = await repository.GetVersionHistoryAsync(state.PluginKey, cancellationToken);
        return new PluginDetailDto(
            MapSummary(state),
            state.EntryPoint,
            state.Permissions,
            state.ConfigurationSchema,
            state.Metadata,
            checks.Select(MapHealth).ToList(),
            versions.Select(MapSummary).ToList());
    }

    public async Task<PluginDetailDto> RegisterAsync(RegisterPluginCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        Validate(command.Key, command.Name, command.Publisher, command.Version, command.ManifestUrl,
            command.PlatformApiVersion, command.ConfigurationSchema);
        var normalizedKey = NormalizeKey(command.Key);
        var existing = await repository.GetByKeyAsync(normalizedKey, cancellationToken);
        if (existing is not null)
            throw new ResourceConflictException("plugin.key.conflict", $"A plugin with key '{normalizedKey}' already exists.");

        Plugin aggregate;
        try
        {
            aggregate = Plugin.Register(
                normalizedKey,
                command.Name,
                command.Description,
                command.Publisher,
                command.Category,
                PluginVersion.FromString(command.Version),
                command.ManifestUrl,
                command.EntryPoint,
                command.PlatformApiVersion,
                command.Capabilities ?? [],
                command.Permissions ?? []);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException("plugin.invalid", exception.Message);
        }

        var state = MapState(
            aggregate,
            aggregate.Id.Value,
            command.ConfigurationSchema,
            command.Metadata ?? new Dictionary<string, string>());
        await repository.AddPluginAsync(state, cancellationToken);
        return await GetPluginAsync(state.Id, cancellationToken);
    }

    public async Task<PluginDetailDto> UpdateAsync(Guid id, UpdatePluginCommand command, CancellationToken cancellationToken = default)
    {
        Validate("existing", command.Name, command.Publisher, "1.0.0", "builtin://existing",
            command.PlatformApiVersion, command.ConfigurationSchema);
        var state = await repository.GetPluginAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{id}' was not found.");
        if (state.Revision != command.Revision)
            throw new ConcurrencyConflictException("plugin", id);
        var aggregate = Restore(state);
        try
        {
            aggregate.UpdateMetadata(
                command.Name,
                command.Description,
                command.Publisher,
                command.Category,
                command.EntryPoint,
                command.PlatformApiVersion,
                command.Capabilities ?? [],
                command.Permissions ?? []);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("plugin.lifecycle.immutable", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException("plugin.invalid", exception.Message);
        }

        var updated = MapState(aggregate, state.PluginKey, command.ConfigurationSchema,
            command.Metadata ?? new Dictionary<string, string>());
        await repository.UpdatePluginAsync(updated, command.Revision, cancellationToken);
        return await GetPluginAsync(id, cancellationToken);
    }

    public async Task<PluginDetailDto> UpdateVersionAsync(Guid id, UpdatePluginVersionCommand command, CancellationToken cancellationToken = default)
    {
        var source = await repository.GetPluginAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{id}' was not found.");
        if (source.Revision != command.Revision)
            throw new ConcurrencyConflictException("plugin", id);
        Validate(source.Key, source.Name, source.Publisher, command.Version, command.ManifestUrl,
            source.PlatformApiVersion, source.ConfigurationSchema);
        var history = await repository.GetVersionHistoryAsync(source.PluginKey, cancellationToken);
        if (history.Any(item => item.Version.Equals(command.Version.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ResourceConflictException("plugin.version.conflict", $"Version '{command.Version}' is already registered for '{source.Name}'.");

        var aggregate = Plugin.Register(
            source.Key,
            source.Name,
            source.Description,
            source.Publisher,
            source.Category,
            PluginVersion.FromString(command.Version),
            command.ManifestUrl,
            source.EntryPoint,
            source.PlatformApiVersion,
            source.Capabilities,
            source.Permissions);
        var next = MapState(aggregate, source.PluginKey, source.ConfigurationSchema, source.Metadata);
        await repository.AddPluginAsync(next, cancellationToken);
        return await GetPluginAsync(next.Id, cancellationToken);
    }

    public async Task<PluginDetailDto> TransitionAsync(Guid id, string action, CancellationToken cancellationToken = default)
    {
        var state = await repository.GetPluginAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{id}' was not found.");

        if (action.Trim().Equals("activate", StringComparison.OrdinalIgnoreCase)
            && !IsCompatible(state.PlatformApiVersion))
            throw new DomainRuleViolationException("plugin.compatibility.unsupported",
                $"Plugin '{state.Name}' targets Platform API {state.PlatformApiVersion}, but this runtime supports {CurrentPlatformApiVersion}.");

        if (action.Trim().Equals("activate", StringComparison.OrdinalIgnoreCase)
            && state.HealthStatus is PluginHealthStatus.Unknown or PluginHealthStatus.Unhealthy)
        {
            await CheckHealthAsync(id, cancellationToken);
            state = await repository.GetPluginAsync(id, cancellationToken)
                ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{id}' was not found.");
        }

        var aggregate = Restore(state);
        try
        {
            switch (action.Trim().ToLowerInvariant())
            {
                case "activate":
                    aggregate.Activate();
                    await ActivateVersionAsync(state, aggregate, cancellationToken);
                    return await GetPluginAsync(id, cancellationToken);
                case "deactivate":
                case "disable":
                    aggregate.Deactivate();
                    break;
                case "deprecate":
                    aggregate.Deprecate();
                    break;
                default:
                    throw new RequestValidationException("plugin.action.invalid", $"Unsupported plugin action '{action}'.");
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("plugin.lifecycle.invalid_transition", exception.Message);
        }

        await repository.UpdatePluginAsync(
            MapState(aggregate, state.PluginKey, state.ConfigurationSchema, state.Metadata),
            state.Revision,
            cancellationToken);
        return await GetPluginAsync(id, cancellationToken);
    }

    public async Task<PluginHealthCheckDto> CheckHealthAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var state = await repository.GetPluginAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{id}' was not found.");
        var result = await healthProbe.ProbeAsync(
            new PluginProbeRequest(state.Key, state.ManifestUrl, state.EntryPoint, state.Category),
            cancellationToken);
        var aggregate = Restore(state);
        aggregate.RecordHealth(result.Status, result.Message, DateTimeOffset.UtcNow);
        var updated = MapState(aggregate, state.PluginKey, state.ConfigurationSchema, state.Metadata);
        var check = new PluginHealthCheckState(
            Guid.NewGuid(), id, result.Status, result.Message, result.DurationMs, result.Source,
            updated.LastHealthCheckAt ?? DateTimeOffset.UtcNow);
        await repository.RecordHealthCheckAsync(
            new PluginUpdateState(updated, state.Revision),
            check,
            cancellationToken);
        return MapHealth(check);
    }

    public async Task<PluginId> RegisterPluginAsync(
        string name,
        string description,
        string version,
        string manifestUrl,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(name);
        if (await repository.GetByKeyAsync(key, cancellationToken) is not null)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6];
            key = $"{key}-{suffix}";
        }
        var plugin = await RegisterAsync(new RegisterPluginCommand(
            key, name, description, "External", version, PluginCategory.Tool,
            manifestUrl, string.Empty, CurrentPlatformApiVersion), cancellationToken);
        return PluginId.FromGuid(plugin.Summary.Id);
    }

    public async Task<bool> DeactivatePluginAsync(PluginId pluginId, CancellationToken cancellationToken = default)
    {
        await TransitionAsync(pluginId.Value, "deactivate", cancellationToken);
        return true;
    }

    public async Task<bool> ActivatePluginAsync(PluginId pluginId, CancellationToken cancellationToken = default)
    {
        await TransitionAsync(pluginId.Value, "activate", cancellationToken);
        return true;
    }

    public async Task<bool> UpdatePluginVersionAsync(
        PluginId pluginId,
        string newVersion,
        string newManifestUrl,
        CancellationToken cancellationToken = default)
    {
        var state = await repository.GetPluginAsync(pluginId.Value, cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{pluginId.Value}' was not found.");
        await UpdateVersionAsync(pluginId.Value, new UpdatePluginVersionCommand(newVersion, newManifestUrl, state.Revision), cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Plugin>> GetActivePluginsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return (await repository.ListPluginsAsync(cancellationToken))
            .Where(item => item.Status == PluginStatus.Active
                           && item.HealthStatus is PluginHealthStatus.Healthy or PluginHealthStatus.Degraded)
            .Select(Restore)
            .ToList();
    }

    private async Task ActivateVersionAsync(
        PluginState state,
        Plugin aggregate,
        CancellationToken cancellationToken)
    {
        var updates = new List<PluginUpdateState>
        {
            new(
                MapState(aggregate, state.PluginKey, state.ConfigurationSchema, state.Metadata),
                state.Revision)
        };
        var versions = await repository.GetVersionHistoryAsync(state.PluginKey, cancellationToken);
        foreach (var active in versions.Where(item => item.Id != state.Id && item.Status == PluginStatus.Active))
        {
            var previous = Restore(active);
            previous.Deactivate();
            updates.Add(new PluginUpdateState(
                MapState(previous, active.PluginKey, active.ConfigurationSchema, active.Metadata),
                active.Revision));
        }

        await repository.UpdatePluginsAsync(updates, cancellationToken);
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        if (await repository.CountPluginsAsync(cancellationToken) > 0) return;
        await SeedGate.WaitAsync(cancellationToken);
        try
        {
            if (await repository.CountPluginsAsync(cancellationToken) > 0) return;
            foreach (var seed in DefaultPlugins())
                await repository.AddPluginAsync(seed, cancellationToken);
        }
        finally
        {
            SeedGate.Release();
        }
    }

    private static IReadOnlyList<PluginState> DefaultPlugins()
    {
        return
        [
            Seed("deterministic-provider", "ConvoLab Deterministic Provider", "Built-in deterministic intelligence adapter for repeatable engineering tests.",
                "ConvoLab", PluginCategory.Provider, "1.0.0", "builtin://intelligence/deterministic", "DeterministicIntelligenceExecutor",
                ["chat-completion", "streaming", "deterministic-execution"], []),
            Seed("local-knowledge-connector", "Local Knowledge Connector", "Built-in file ingestion adapter for text, PDF, and DOCX knowledge assets.",
                "ConvoLab", PluginCategory.KnowledgeConnector, "1.0.0", "builtin://knowledge/local-files", "LocalKnowledgeDocumentStorage",
                ["text", "pdf", "docx", "chunking", "keyword-retrieval"], ["filesystem.read"]),
            Seed("evaluation-metrics-pack", "Evaluation Metrics Pack", "Built-in groundedness, relevance, safety, and completeness evaluators.",
                "ConvoLab", PluginCategory.Evaluator, "1.0.0", "builtin://evaluation/default", "EvaluationEngine",
                ["groundedness", "relevance", "safety", "completeness"], []),
            Seed("persistent-trace-exporter", "Persistent Trace Exporter", "Built-in trace adapter that records spans, events, and governed artifacts.",
                "ConvoLab", PluginCategory.TraceExporter, "1.0.0", "builtin://tracing/persistent", "PersistentTraceEngine",
                ["spans", "events", "artifacts", "correlation"], ["database.write"])
        ];
    }

    private static PluginState Seed(
        string key,
        string name,
        string description,
        string publisher,
        PluginCategory category,
        string version,
        string manifestUrl,
        string entryPoint,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<string> permissions)
    {
        var aggregate = Plugin.Register(key, name, description, publisher, category,
            PluginVersion.FromString(version), manifestUrl, entryPoint, CurrentPlatformApiVersion,
            capabilities, permissions);
        aggregate.RecordHealth(PluginHealthStatus.Healthy, "Built-in adapter is available in the current process.", DateTimeOffset.UtcNow);
        aggregate.Activate();
        return MapState(aggregate, aggregate.Id.Value, "{}", new Dictionary<string, string>
        {
            ["distribution"] = "built-in",
            ["trustLevel"] = "platform"
        });
    }

    private static Plugin Restore(PluginState state)
        => Plugin.Restore(
            PluginId.FromGuid(state.Id), state.Key, state.Name, state.Description, state.Publisher,
            state.Category, PluginVersion.FromString(state.Version), state.ManifestUrl, state.EntryPoint,
            state.PlatformApiVersion, state.Capabilities, state.Permissions, state.Status,
            state.HealthStatus, state.HealthMessage, state.LastHealthCheckAt, state.Revision,
            state.CreatedAt, state.UpdatedAt);

    private static PluginState MapState(
        Plugin aggregate,
        Guid pluginKey,
        string configurationSchema,
        IReadOnlyDictionary<string, string> metadata)
        => new(
            aggregate.Id.Value, pluginKey, aggregate.Key, aggregate.Name, aggregate.Description,
            aggregate.Publisher, aggregate.Version.Value, aggregate.Category, aggregate.Status,
            aggregate.HealthStatus, aggregate.HealthMessage, aggregate.ManifestUrl, aggregate.EntryPoint,
            aggregate.PlatformApiVersion, aggregate.Capabilities.ToList(), aggregate.Permissions.ToList(),
            configurationSchema, metadata.ToDictionary(item => item.Key, item => item.Value),
            aggregate.LastHealthCheckAt, aggregate.Revision, aggregate.CreatedAt, aggregate.UpdatedAt);

    private static PluginSummaryDto MapSummary(PluginState state)
        => new(
            state.Id, state.Key, state.Name, state.Description, state.Publisher, state.Version,
            state.Category, state.Status, state.HealthStatus, state.HealthMessage, state.ManifestUrl,
            state.PlatformApiVersion, IsCompatible(state.PlatformApiVersion), state.Capabilities,
            state.LastHealthCheckAt, state.UpdatedAt, state.Revision);

    private static PluginHealthCheckDto MapHealth(PluginHealthCheckState state)
        => new(state.Id, state.PluginId, state.Status, state.Message, state.DurationMs, state.Source, state.CheckedAt);

    private static bool IsCompatible(string requested)
        => TryMajor(requested, out var requestedMajor)
           && TryMajor(CurrentPlatformApiVersion, out var platformMajor)
           && requestedMajor == platformMajor;

    private static bool TryMajor(string value, out int major)
    {
        var segment = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(segment, out major);
    }

    private static void Validate(
        string key,
        string name,
        string publisher,
        string version,
        string manifestUrl,
        string platformApiVersion,
        string configurationSchema)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(publisher))
            throw new RequestValidationException("plugin.required", "Plugin key, name, and publisher are required.");
        if (!KeyPattern().IsMatch(NormalizeKey(key)))
            throw new RequestValidationException("plugin.key.invalid", "Plugin keys may contain lowercase letters, numbers, and hyphens.");
        try { _ = PluginVersion.FromString(version); }
        catch (ArgumentException exception) { throw new RequestValidationException("plugin.version.invalid", exception.Message); }
        if (!VersionPattern().IsMatch(version.Trim()))
            throw new RequestValidationException("plugin.version.invalid", "Plugin version must use semantic version format, for example 1.0.0.");
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri)
            || (manifestUri.Scheme != "builtin" && manifestUri.Scheme != Uri.UriSchemeHttp && manifestUri.Scheme != Uri.UriSchemeHttps))
            throw new RequestValidationException("plugin.manifest.invalid", "Manifest URL must be an absolute builtin, HTTP, or HTTPS URI.");
        if (!TryMajor(platformApiVersion, out _))
            throw new RequestValidationException("plugin.api_version.invalid", "Platform API version must begin with a numeric major version.");
        try { using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(configurationSchema) ? "{}" : configurationSchema); }
        catch (JsonException exception) { throw new RequestValidationException("plugin.configuration_schema.invalid", $"Configuration schema must be valid JSON. {exception.Message}"); }
    }

    private static string NormalizeKey(string value)
    {
        var normalized = SlugPattern().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "plugin" : normalized;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}

