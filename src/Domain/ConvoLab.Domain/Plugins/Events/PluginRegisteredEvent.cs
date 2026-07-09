using ConvoLab.Domain.Events;
using ConvoLab.Domain.Plugins.ValueObjects;
namespace ConvoLab.Domain.Plugins.Events;
public record PluginRegisteredEvent(PluginId PluginId, string Name, PluginVersion Version) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
