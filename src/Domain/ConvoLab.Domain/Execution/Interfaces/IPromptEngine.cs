using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IPromptEngine
{
    Task<string> RenderPromptAsync(PromptTemplateId templateId, Dictionary<string, string> variables, ValueObjects.ExecutionContext context);
}
