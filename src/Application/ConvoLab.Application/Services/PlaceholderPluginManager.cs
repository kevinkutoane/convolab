using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Plugins.ValueObjects;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Domain.Plugins.Aggregates;
namespace ConvoLab.Application.Services;
public class PlaceholderPluginManager : IPluginManager {
    public Task<PluginId> RegisterPluginAsync(string name, string description, string version, string manifestUrl, CancellationToken cancellationToken = default) => Task.FromResult(PluginId.CreateUnique());
    public Task<bool> DeactivatePluginAsync(PluginId pluginId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> ActivatePluginAsync(PluginId pluginId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> UpdatePluginVersionAsync(PluginId pluginId, string newVersion, string newManifestUrl, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<IReadOnlyList<Plugin>> GetActivePluginsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Plugin>>(new List<Plugin>());
}
