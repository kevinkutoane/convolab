using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.ValueObjects;
using PromptAggregate = ConvoLab.Domain.Prompt.Aggregates.Prompt;

namespace ConvoLab.Domain.Prompt.Interfaces;

/// <summary>
/// Repository contract for the Prompt aggregate.
/// Implementations reside in the Infrastructure layer.
/// </summary>
public interface IPromptRepository
{
    Task<PromptAggregate?> GetByIdAsync(PromptId id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PromptAggregate>> GetByStatusAsync(PromptStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<PromptAggregate>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task AddAsync(PromptAggregate prompt, CancellationToken cancellationToken = default);
    Task UpdateAsync(PromptAggregate prompt, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(PromptId id, CancellationToken cancellationToken = default);
}
