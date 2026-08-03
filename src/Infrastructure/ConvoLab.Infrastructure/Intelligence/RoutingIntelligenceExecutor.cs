using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;
using ConvoLab.Application.Operations;

namespace ConvoLab.Infrastructure.Intelligence;

public sealed class RoutingIntelligenceExecutor : IIntelligenceExecutor
{
    private readonly DeterministicIntelligenceExecutor _deterministic;
    private readonly GeminiIntelligenceExecutor _gemini;
    private readonly IPlatformOperationalState _operationalState;
    public IReadOnlyCollection<ProviderKind> SupportedProviders { get; } = [ProviderKind.InternalModel, ProviderKind.Gemini];

    public RoutingIntelligenceExecutor(
        DeterministicIntelligenceExecutor deterministic,
        GeminiIntelligenceExecutor gemini,
        IPlatformOperationalState operationalState)
    {
        _deterministic = deterministic;
        _gemini = gemini;
        _operationalState = operationalState;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string renderedPrompt,
        CancellationToken cancellationToken = default)
    {
        if (renderedPrompt.Contains("[PROVIDER:Gemini]", StringComparison.OrdinalIgnoreCase))
        {
            await _operationalState.EnsureExternalExecutionAllowedAsync(cancellationToken);
            return await _gemini.ExecuteAsync(request, renderedPrompt, cancellationToken);
        }

        await _operationalState.EnsureDeterministicExecutionAllowedAsync(cancellationToken);
        return await _deterministic.ExecuteAsync(request, renderedPrompt, cancellationToken);
    }
}
