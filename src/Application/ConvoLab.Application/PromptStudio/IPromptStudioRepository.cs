using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Application.PromptStudio;

public sealed record PromptDefinitionState(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    string Category,
    IReadOnlyList<string> Tags,
    PromptStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record PromptVersionState(
    Guid Id,
    Guid PromptId,
    string Version,
    PromptStatus Status,
    string ChangeSummary,
    IReadOnlyList<PromptTemplateSection> Sections,
    IReadOnlyList<string> Variables,
    int EstimatedTokens,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    long Revision);

public sealed record PromptLifecycleState(
    Guid Id,
    Guid PromptVersionId,
    string Actor,
    string Action,
    string? Reason,
    PromptStatus PreviousStatus,
    PromptStatus NewStatus,
    DateTimeOffset CreatedAt);

public interface IPromptStudioRepository
{
    Task<IReadOnlyList<PromptDefinitionState>> ListPromptsAsync(CancellationToken ct = default);
    Task<PromptDefinitionState?> GetPromptAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PromptVersionState>> ListVersionsAsync(Guid promptId, CancellationToken ct = default);
    Task<PromptVersionState?> GetVersionAsync(Guid id, CancellationToken ct = default);
    Task<bool> VersionExistsAsync(Guid promptId, string semanticVersion, CancellationToken ct = default);
    Task<IReadOnlyList<PromptVersionState>> ListPublishedVersionsAsync(CancellationToken ct = default);
    Task AddPromptAsync(PromptDefinitionState prompt, CancellationToken ct = default);
    Task UpdatePromptAsync(PromptDefinitionState prompt, long expectedRevision, CancellationToken ct = default);
    Task AddVersionAsync(PromptVersionState version, CancellationToken ct = default);
    Task UpdateVersionAsync(PromptVersionState version, long expectedRevision, CancellationToken ct = default);
    Task AddLifecycleEntryAsync(PromptLifecycleState entry, CancellationToken ct = default);
}
