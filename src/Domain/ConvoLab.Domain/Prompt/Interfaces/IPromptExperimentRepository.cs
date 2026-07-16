using ConvoLab.Domain.Prompt.Aggregates;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Interfaces;

/// <summary>
/// Repository contract for the PromptExperiment aggregate.
/// </summary>
public interface IPromptExperimentRepository
{
    Task<PromptExperiment?> GetByIdAsync(PromptExperimentId id, CancellationToken cancellationToken = default);
    Task AddAsync(PromptExperiment experiment, CancellationToken cancellationToken = default);
    Task UpdateAsync(PromptExperiment experiment, CancellationToken cancellationToken = default);
}
