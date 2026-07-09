using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.AI.ValueObjects;
using ConvoLab.Domain.Prompt.ValueObjects;
namespace ConvoLab.Application.Services;
public class PlaceholderAIOrchestrator : IAIOrchestrator {
    public Task<string> ProcessPromptAsync(AIModelId modelId, PromptTemplateId promptTemplateId, Dictionary<string, string> promptParameters, CancellationToken cancellationToken = default) => Task.FromResult("AI Orchestrator response");
    public Task<string> ProcessFreeformTextAsync(AIModelId modelId, string freeformText, CancellationToken cancellationToken = default) => Task.FromResult("AI Orchestrator freeform response");
}
