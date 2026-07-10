using ConvoLab.Domain.Common;
using ConvoLab.Domain.Plugins.ValueObjects;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Domain.Plugins.Events;
namespace ConvoLab.Domain.Plugins.Aggregates;
public class Plugin : BaseAggregateRoot<PluginId> {
    public string Name { get; private set; }
    public string Description { get; private set; }
    public PluginVersion Version { get; private set; }
    public string ManifestUrl { get; private set; }
    public PluginStatus Status { get; private set; }
    private Plugin() : base() { }
    private Plugin(PluginId id, string name, string description, PluginVersion version, string manifestUrl) : base(id) {
        Name = name; Description = description; Version = version; ManifestUrl = manifestUrl; Status = PluginStatus.Active;
        AddDomainEvent(new PluginRegisteredEvent(id, name, version));
    }
    public static Plugin Register(string name, string description, PluginVersion version, string manifestUrl) => new Plugin(PluginId.CreateUnique(), name, description, version, manifestUrl);
}
