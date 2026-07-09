using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Prompt.Enums;
namespace ConvoLab.Application.Common.Interfaces;
public interface IPromptEngine {
    Task<string> GeneratePromptAsync(PromptTemplateId templateId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
    Task<PromptTemplateId> CreatePromptTemplateAsync(string name, string templateString, PromptType type, string version, CancellationToken cancellationToken = default);
    Task<bool> UpdatePromptTemplateAsync(PromptTemplateId templateId, string newTemplateString, string newVersion, CancellationToken cancellationToken = default);
    Task<bool> DeactivatePromptTemplateAsync(PromptTemplateId templateId, CancellationToken cancellationToken = default);
    Task<bool> ActivatePromptTemplateAsync(PromptTemplateId templateId, CancellationToken cancellationToken = default);
}
