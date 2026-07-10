using ConvoLab.Domain.AI.ValueObjects;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IAIOrchestrator
{
    Task<AICompletion> GetCompletionAsync(string prompt, AIModelId modelId, ValueObjects.ExecutionContext context);
    Task<AIEmbedding> GetEmbeddingAsync(string text, AIModelId modelId, ValueObjects.ExecutionContext context);
}
