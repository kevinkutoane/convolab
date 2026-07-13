using ConvoLab.Domain.Plugins.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IPluginManager
{
    Task<ExecutionResult> ExecutePluginAsync(PluginId pluginId, Dictionary<string, string> inputs, ValueObjects.ExecutionContext context);
}
