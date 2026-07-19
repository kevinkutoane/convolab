using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Infrastructure.Intelligence;

public sealed class RoutingIntelligenceExecutor : IIntelligenceExecutor
{
    private readonly DeterministicIntelligenceExecutor _deterministic;
    private readonly GeminiIntelligenceExecutor _gemini;
    public IReadOnlyCollection<ProviderKind> SupportedProviders { get; } = [ProviderKind.InternalModel, ProviderKind.Gemini];

    public RoutingIntelligenceExecutor(DeterministicIntelligenceExecutor deterministic, GeminiIntelligenceExecutor gemini)
    {
        _deterministic = deterministic;
        _gemini = gemini;
    }

    public Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, string renderedPrompt, CancellationToken cancellationToken = default)
        => renderedPrompt.Contains("[PROVIDER:Gemini]", StringComparison.OrdinalIgnoreCase)
            ? _gemini.ExecuteAsync(request, renderedPrompt, cancellationToken)
            : _deterministic.ExecuteAsync(request, renderedPrompt, cancellationToken);
}
