using ConvoLab.Domain.AI.ValueObjects;

namespace ConvoLab.Domain.AI.Interfaces;

public interface IAIOrchestrationService
{
    Task<AICompletion> ExecuteCompletionAsync(CompletionRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<float>> ExecuteEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AICompletionChunk> ExecuteStreamingCompletionAsync(CompletionRequest request, CancellationToken cancellationToken = default);
    Task<AICompletion> ExecuteToolCallAsync(ToolCallRequest request, CancellationToken cancellationToken = default);
}

public record AICompletionChunk(string Content, TokenUsage? Usage);
public record ToolCallRequest(AIModelId ModelId, string ToolName, string Arguments, IEnumerable<AICompletion> History);
