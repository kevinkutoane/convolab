using ConvoLab.Domain.AI.ValueObjects;
using ConvoLab.Domain.Prompt.ValueObjects;
namespace ConvoLab.Application.Common.Interfaces;
public interface IAIOrchestrator {
    Task<string> ProcessPromptAsync(AIModelId modelId, PromptTemplateId promptTemplateId, Dictionary<string, string> promptParameters, CancellationToken cancellationToken = default);
    Task<string> ProcessFreeformTextAsync(AIModelId modelId, string freeformText, CancellationToken cancellationToken = default);
}
