using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Prompt.Enums;
namespace ConvoLab.Application.Services;
public class PlaceholderPromptEngine : IPromptEngine {
    public Task<string> GeneratePromptAsync(PromptTemplateId templateId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default) => Task.FromResult("Generated prompt");
    public Task<PromptTemplateId> CreatePromptTemplateAsync(string name, string templateString, PromptType type, string version, CancellationToken cancellationToken = default) => Task.FromResult(new PromptTemplateId(Guid.NewGuid()));
    public Task<bool> UpdatePromptTemplateAsync(PromptTemplateId templateId, string newTemplateString, string newVersion, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> DeactivatePromptTemplateAsync(PromptTemplateId templateId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> ActivatePromptTemplateAsync(PromptTemplateId templateId, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
