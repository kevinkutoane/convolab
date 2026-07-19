namespace ConvoLab.Application.PromptStudio;

public enum PromptStudioStatus { Draft, PendingApproval, Approved, Published, Deprecated, Archived }
public enum PromptSectionKind { System, Developer, Knowledge, Conversation, User, Output }

public sealed record PromptSectionDto(
    Guid Id,
    PromptSectionKind Kind,
    string Name,
    string Content,
    int Sequence,
    bool Required);

public sealed record PromptVersionDto(
    Guid Id,
    Guid PromptId,
    string Version,
    PromptStudioStatus Status,
    string ChangeSummary,
    IReadOnlyList<PromptSectionDto> Sections,
    IReadOnlyList<string> Variables,
    int EstimatedTokens,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    long Revision);

public sealed record PromptSummaryDto(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    string Category,
    IReadOnlyList<string> Tags,
    PromptStudioStatus Status,
    string LatestVersion,
    int VersionCount,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record PromptDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    string Category,
    IReadOnlyList<string> Tags,
    PromptStudioStatus Status,
    IReadOnlyList<PromptVersionDto> Versions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record CreatePromptCommand(
    string Name,
    string Description,
    string Owner,
    string Category,
    IReadOnlyList<string>? Tags);

public sealed record UpdatePromptCommand(
    string? Name,
    string? Description,
    string? Owner,
    string? Category,
    IReadOnlyList<string>? Tags,
    long? ExpectedRevision = null);

public sealed record CreatePromptVersionCommand(
    string Version,
    string ChangeSummary,
    IReadOnlyList<PromptSectionInput> Sections,
    long? ExpectedPromptRevision = null);

public sealed record PromptSectionInput(
    PromptSectionKind Kind,
    string Name,
    string Content,
    int Sequence,
    bool Required = true);

public sealed record PromptLifecycleCommand(
    string Actor,
    string? Reason = null,
    long? ExpectedRevision = null);

public sealed record RenderPromptCommand(
    Guid VersionId,
    IReadOnlyDictionary<string, string>? Variables);

public sealed record RenderedPromptDto(
    Guid PromptId,
    Guid VersionId,
    string PromptName,
    string Version,
    string RenderedText,
    IReadOnlyList<string> MissingVariables,
    int EstimatedTokens);

public sealed record PromptComparisonDto(
    PromptVersionDto Left,
    PromptVersionDto Right,
    int TokenDelta,
    IReadOnlyList<string> AddedVariables,
    IReadOnlyList<string> RemovedVariables);

public sealed record RuntimePromptTemplate(
    Guid PromptId,
    Guid VersionId,
    string Name,
    string Version,
    string DisplayName,
    IReadOnlyList<PromptSectionDto> Sections);

public interface IPromptStudioService
{
    Task<IReadOnlyList<PromptSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<PromptDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PromptDetailDto> CreateAsync(CreatePromptCommand command, CancellationToken ct = default);
    Task<PromptDetailDto?> UpdateAsync(Guid id, UpdatePromptCommand command, CancellationToken ct = default);
    Task<PromptVersionDto> CreateVersionAsync(Guid promptId, CreatePromptVersionCommand command, CancellationToken ct = default);
    Task<PromptVersionDto?> TransitionAsync(Guid versionId, string action, PromptLifecycleCommand command, CancellationToken ct = default);
    Task<RenderedPromptDto> RenderAsync(RenderPromptCommand command, CancellationToken ct = default);
    Task<PromptComparisonDto> CompareAsync(Guid leftVersionId, Guid rightVersionId, CancellationToken ct = default);
    Task<IReadOnlyList<RuntimePromptTemplate>> ListPublishedAsync(CancellationToken ct = default);
    Task<RuntimePromptTemplate?> ResolvePublishedAsync(string displayName, CancellationToken ct = default);
    string RenderRuntime(RuntimePromptTemplate template, IReadOnlyDictionary<string, string> variables);
}
