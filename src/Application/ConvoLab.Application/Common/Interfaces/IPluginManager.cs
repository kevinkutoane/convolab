using ConvoLab.Domain.Plugins.ValueObjects;
using ConvoLab.Domain.Plugins.Enums;
namespace ConvoLab.Application.Common.Interfaces;
public interface IPluginManager {
    Task<PluginId> RegisterPluginAsync(string name, string description, string version, string manifestUrl, CancellationToken cancellationToken = default);
    Task<bool> DeactivatePluginAsync(PluginId pluginId, CancellationToken cancellationToken = default);
    Task<bool> ActivatePluginAsync(PluginId pluginId, CancellationToken cancellationToken = default);
    Task<bool> UpdatePluginVersionAsync(PluginId pluginId, string newVersion, string newManifestUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Domain.Plugins.Aggregates.Plugin>> GetActivePluginsAsync(CancellationToken cancellationToken = default);
}
