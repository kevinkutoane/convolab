using PromptAggregate = ConvoLab.Domain.Prompt.Aggregates.Prompt;

namespace ConvoLab.Domain.Prompt.Interfaces;

/// <summary>
/// Domain service for rendering prompts. Remains provider-agnostic.
/// The rendering service resolves variables from the execution context and
/// delegates to the Prompt aggregate for the actual substitution.
/// </summary>
public interface IPromptRenderingService
{
    /// <summary>
    /// Renders a prompt by injecting the provided variables into the active version's content.
    /// </summary>
    Task<string> RenderAsync(PromptAggregate prompt, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a composed prompt by assembling all enabled sections in order.
    /// </summary>
    Task<string> RenderComposedAsync(PromptAggregate prompt, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the token count for a rendered prompt without actually rendering it.
    /// Used for cost estimation and constraint validation.
    /// </summary>
    Task<int> EstimateTokensAsync(PromptAggregate prompt, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default);
}
