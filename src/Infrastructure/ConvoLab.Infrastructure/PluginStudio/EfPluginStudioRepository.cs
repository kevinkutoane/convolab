using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.PluginStudio;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.PluginStudio;

public sealed class EfPluginStudioRepository(ApplicationDbContext db) : IPluginStudioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<int> CountPluginsAsync(CancellationToken cancellationToken = default)
        => db.Plugins.AsNoTracking().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<PluginState>> ListPluginsAsync(CancellationToken cancellationToken = default)
    {
        var query = db.Plugins.AsNoTracking();
        var records = db.Database.IsSqlite()
            ? (await query.ToListAsync(cancellationToken))
                .OrderBy(item => item.Name)
                .ThenByDescending(item => item.UpdatedAt)
                .ToList()
            : await query
                .OrderBy(item => item.Name)
                .ThenByDescending(item => item.UpdatedAt)
                .ToListAsync(cancellationToken);
        return records.Select(Map).ToList();
    }

    public async Task<PluginState?> GetPluginAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.Plugins.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Map(record);
    }

    public async Task<PluginState?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var query = db.Plugins.AsNoTracking().Where(item => item.Key == normalized);
        var record = db.Database.IsSqlite()
            ? (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.UpdatedAt).FirstOrDefault()
            : await query.OrderByDescending(item => item.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : Map(record);
    }

    public async Task<IReadOnlyList<PluginState>> GetVersionHistoryAsync(Guid pluginKey, CancellationToken cancellationToken = default)
    {
        var query = db.Plugins.AsNoTracking().Where(item => item.PluginKey == pluginKey);
        var records = db.Database.IsSqlite()
            ? (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).ToList()
            : await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return records.Select(Map).ToList();
    }

    public async Task AddPluginAsync(PluginState plugin, CancellationToken cancellationToken = default)
    {
        db.Plugins.Add(MapRecord(plugin));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ResourceConflictException("plugin.version.conflict", $"Plugin '{plugin.Key}' version '{plugin.Version}' already exists. {exception.GetBaseException().Message}");
        }
    }

    public Task UpdatePluginAsync(PluginState plugin, long expectedRevision, CancellationToken cancellationToken = default)
        => UpdatePluginsAsync([new PluginUpdateState(plugin, expectedRevision)], cancellationToken);

    public async Task UpdatePluginsAsync(
        IReadOnlyList<PluginUpdateState> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return;
        var duplicate = updates.GroupBy(item => item.Plugin.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ResourceConflictException("plugin.update.duplicate", $"Plugin '{duplicate.Key}' was included more than once in the same update.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ids = updates.Select(item => item.Plugin.Id).ToList();
        var records = await db.Plugins
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var update in updates)
        {
            if (!records.TryGetValue(update.Plugin.Id, out var record))
                throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{update.Plugin.Id}' was not found.");
            if (record.Revision != update.ExpectedRevision)
                throw new ConcurrencyConflictException("plugin", update.Plugin.Id);
        }

        try
        {
            // Release the unique active-version slot before activating a successor.
            var deactivationIds = updates
                .Where(item => records[item.Plugin.Id].Status == PluginStatus.Active.ToString()
                               && item.Plugin.Status != PluginStatus.Active)
                .Select(item => item.Plugin.Id)
                .ToHashSet();
            foreach (var update in updates.Where(item => deactivationIds.Contains(item.Plugin.Id)))
                Apply(records[update.Plugin.Id], update.Plugin);
            if (deactivationIds.Count > 0)
                await db.SaveChangesAsync(cancellationToken);

            foreach (var update in updates.Where(item => !deactivationIds.Contains(item.Plugin.Id)))
                Apply(records[update.Plugin.Id], update.Plugin);
            if (updates.Count > deactivationIds.Count)
                await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("plugin", updates[0].Plugin.Id);
        }
        catch (DbUpdateException exception)
        {
            throw new ResourceConflictException("plugin.activation.conflict",
                $"The plugin lifecycle changed concurrently. {exception.GetBaseException().Message}");
        }
    }

    public async Task<IReadOnlyList<PluginHealthCheckState>> ListHealthChecksAsync(
        int limit = 100,
        Guid? pluginId = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.PluginHealthChecks.AsNoTracking().AsQueryable();
        if (pluginId.HasValue) query = query.Where(item => item.PluginId == pluginId.Value);
        var boundedLimit = Math.Clamp(limit, 1, 500);
        var records = db.Database.IsSqlite()
            ? (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CheckedAt).Take(boundedLimit).ToList()
            : await query.OrderByDescending(item => item.CheckedAt).Take(boundedLimit).ToListAsync(cancellationToken);
        return records.Select(Map).ToList();
    }

    public async Task AddHealthCheckAsync(PluginHealthCheckState healthCheck, CancellationToken cancellationToken = default)
    {
        db.PluginHealthChecks.Add(ToRecord(healthCheck));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordHealthCheckAsync(
        PluginUpdateState update,
        PluginHealthCheckState healthCheck,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = await db.Plugins.SingleOrDefaultAsync(
            item => item.Id == update.Plugin.Id,
            cancellationToken)
            ?? throw new ResourceNotFoundException("plugin.not_found", $"Plugin '{update.Plugin.Id}' was not found.");
        if (record.Revision != update.ExpectedRevision)
            throw new ConcurrencyConflictException("plugin", update.Plugin.Id);

        Apply(record, update.Plugin);
        db.PluginHealthChecks.Add(ToRecord(healthCheck));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("plugin", update.Plugin.Id);
        }
    }


    private static void Apply(PluginRecord record, PluginState plugin)
    {
        record.Name = plugin.Name;
        record.Description = plugin.Description;
        record.Publisher = plugin.Publisher;
        record.Version = plugin.Version;
        record.Category = plugin.Category.ToString();
        record.Status = plugin.Status.ToString();
        record.HealthStatus = plugin.HealthStatus.ToString();
        record.HealthMessage = plugin.HealthMessage;
        record.ManifestUrl = plugin.ManifestUrl;
        record.EntryPoint = plugin.EntryPoint;
        record.PlatformApiVersion = plugin.PlatformApiVersion;
        record.CapabilitiesJson = JsonSerializer.Serialize(plugin.Capabilities, JsonOptions);
        record.PermissionsJson = JsonSerializer.Serialize(plugin.Permissions, JsonOptions);
        record.ConfigurationSchema = plugin.ConfigurationSchema;
        record.MetadataJson = JsonSerializer.Serialize(plugin.Metadata, JsonOptions);
        record.LastHealthCheckAt = plugin.LastHealthCheckAt;
        record.Revision = plugin.Revision;
        record.UpdatedAt = plugin.UpdatedAt;
    }


    private static PluginHealthCheckRecord ToRecord(PluginHealthCheckState healthCheck) => new()
    {
        Id = healthCheck.Id,
        PluginId = healthCheck.PluginId,
        Status = healthCheck.Status.ToString(),
        Message = healthCheck.Message,
        DurationMs = healthCheck.DurationMs,
        Source = healthCheck.Source,
        CheckedAt = healthCheck.CheckedAt
    };

    private static PluginState Map(PluginRecord record)
        => new(
            record.Id, record.PluginKey, record.Key, record.Name, record.Description, record.Publisher,
            record.Version, Parse(record.Category, PluginCategory.Tool), Parse(record.Status, PluginStatus.Installed),
            Parse(record.HealthStatus, PluginHealthStatus.Unknown), record.HealthMessage, record.ManifestUrl,
            record.EntryPoint, record.PlatformApiVersion, DeserializeList(record.CapabilitiesJson),
            DeserializeList(record.PermissionsJson), record.ConfigurationSchema, DeserializeDictionary(record.MetadataJson),
            record.LastHealthCheckAt, record.Revision, record.CreatedAt, record.UpdatedAt);

    private static PluginHealthCheckState Map(PluginHealthCheckRecord record)
        => new(record.Id, record.PluginId, Parse(record.Status, PluginHealthStatus.Unknown), record.Message,
            record.DurationMs, record.Source, record.CheckedAt);

    private static PluginRecord MapRecord(PluginState state) => new()
    {
        Id = state.Id,
        PluginKey = state.PluginKey,
        Key = state.Key,
        Name = state.Name,
        Description = state.Description,
        Publisher = state.Publisher,
        Version = state.Version,
        Category = state.Category.ToString(),
        Status = state.Status.ToString(),
        HealthStatus = state.HealthStatus.ToString(),
        HealthMessage = state.HealthMessage,
        ManifestUrl = state.ManifestUrl,
        EntryPoint = state.EntryPoint,
        PlatformApiVersion = state.PlatformApiVersion,
        CapabilitiesJson = JsonSerializer.Serialize(state.Capabilities, JsonOptions),
        PermissionsJson = JsonSerializer.Serialize(state.Permissions, JsonOptions),
        ConfigurationSchema = state.ConfigurationSchema,
        MetadataJson = JsonSerializer.Serialize(state.Metadata, JsonOptions),
        LastHealthCheckAt = state.LastHealthCheckAt,
        Revision = state.Revision,
        CreatedAt = state.CreatedAt,
        UpdatedAt = state.UpdatedAt
    };

    private static IReadOnlyList<string> DeserializeList(string json)
        => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static IReadOnlyDictionary<string, string> DeserializeDictionary(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();

    private static T Parse<T>(string value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;
}
