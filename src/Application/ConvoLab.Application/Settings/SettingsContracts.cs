using ConvoLab.Domain.Settings;

namespace ConvoLab.Application.Settings;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record EnvironmentDto(
    Guid Id,
    Guid OrganisationId,
    Guid WorkspaceId,
    string Name,
    string Slug,
    string EnvironmentType,
    string Description,
    string Status,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record SettingValueDto(
    Guid Id,
    string DefinitionKey,
    string DisplayName,
    string Category,
    string Scope,
    string? OrganisationId,
    string? WorkspaceId,
    string? EnvironmentId,
    string ValueJson,
    bool IsSecret,
    string ValueType,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record EffectiveSettingDto(
    string Key,
    string? EffectiveValue,
    string ValueType,
    string SourceScope,
    string? SourceId,
    bool IsInherited,
    bool IsSecret,
    string ValidationStatus,
    bool RequiresRestart,
    string DisplayName,
    string Category,
    string? InheritedFromDisplay,
    string Description,
    bool IsRequired,
    IReadOnlyList<string> AllowedValues,
    bool AllowsEnvironmentOverride);

public sealed record SecretReferenceDto(
    Guid Id,
    Guid WorkspaceId,
    string DisplayName,
    string Reference,
    string Provider,
    string Status,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationOutcome,
    bool IsDisabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record ConfigurationChangeDto(
    Guid Id,
    string SettingKey,
    string? PreviousValueSummary,
    string NewValueSummary,
    string ChangedByDisplay,
    DateTimeOffset ChangedAt,
    string? Reason,
    string CorrelationId,
    string Outcome,
    string? EnvironmentName);

public sealed record ProviderValidationResultDto(
    string Outcome,
    string Message,
    bool SecretResolved,
    bool ProviderReachable,
    bool AuthSucceeded,
    bool ModelAvailable,
    int DurationMs);

public sealed record ConfigurationExportDto(
    string SchemaVersion,
    string Organisation,
    string Workspace,
    string Environment,
    DateTimeOffset ExportedAt,
    IReadOnlyList<ExportedSettingDto> Settings,
    IReadOnlyList<ExportedFeatureFlagDto> FeatureFlags,
    ExportedProviderMetadataDto? ProviderMetadata);

public sealed record ExportedSettingDto(string Key, string Category, string DisplayName, string? Value);
public sealed record ExportedFeatureFlagDto(string Key, string? Value);
public sealed record ExportedProviderMetadataDto(string? Provider, string? Model, bool ProviderEnabled);

// ─── Commands / Requests ─────────────────────────────────────────────────────

public sealed record CreateEnvironmentRequest(
    string Name,
    string Slug,
    string EnvironmentType,
    string? Description,
    bool IsDefault);

public sealed record UpdateEnvironmentRequest(
    string Name,
    string? Description,
    string EnvironmentType,
    long ExpectedRevision);

public sealed record UpsertSettingRequest(
    string ValueJson,
    string? Reason,
    long? ExpectedRevision,
    bool ConfirmProtectedChange = false);

public sealed record CreateSecretReferenceRequest(
    string DisplayName,
    string Reference);

public sealed record UpdateSecretReferenceRequest(
    string DisplayName,
    string Reference,
    long ExpectedRevision);

public sealed record ImportConfigurationRequest(
    string SettingsJson,
    bool ValidateOnly,
    string? Reason);

public sealed record SettingValidationEntryDto(
    string Key,
    string DisplayName,
    string Category,
    string Status,
    string? Message,
    string SourceScope);

public sealed record SettingsValidationResultDto(
    bool IsValid,
    int CheckedCount,
    int InvalidCount,
    int WarningCount,
    IReadOnlyList<SettingValidationEntryDto> Entries,
    DateTimeOffset ValidatedAt);

// ─── Interfaces ───────────────────────────────────────────────────────────────

public interface IEnvironmentService
{
    Task<IReadOnlyList<EnvironmentDto>> ListAsync(Guid workspaceId, CancellationToken ct = default);
    Task<EnvironmentDto> GetAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default);
    Task<EnvironmentDto> CreateAsync(Guid workspaceId, CreateEnvironmentRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<EnvironmentDto> UpdateAsync(Guid workspaceId, Guid environmentId, UpdateEnvironmentRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task ActivateAsync(Guid workspaceId, Guid environmentId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task SuspendAsync(Guid workspaceId, Guid environmentId, long expectedRevision, bool isAdmin, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task ArchiveAsync(Guid workspaceId, Guid environmentId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task MakeDefaultAsync(Guid workspaceId, Guid environmentId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<EnvironmentDto> SelectAsync(Guid workspaceId, Guid environmentId, Guid actorId, string actorType, string? actorRole, string correlationId, CancellationToken ct = default);
}

public interface ISettingsService
{
    Task<IReadOnlyList<SettingValueDto>> ListWorkspaceSettingsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<IReadOnlyList<EffectiveSettingDto>> GetEffectiveWorkspaceSettingsAsync(Guid workspaceId, Guid? environmentId, CancellationToken ct = default);
    Task<SettingValueDto> UpsertWorkspaceSettingAsync(Guid workspaceId, string settingKey, UpsertSettingRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task DeleteWorkspaceSettingAsync(Guid workspaceId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<IReadOnlyList<SettingValueDto>> ListEnvironmentSettingsAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default);
    Task<IReadOnlyList<EffectiveSettingDto>> GetEffectiveEnvironmentSettingsAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default);
    Task<SettingValueDto> UpsertEnvironmentSettingAsync(Guid workspaceId, Guid environmentId, string settingKey, UpsertSettingRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task DeleteEnvironmentSettingAsync(Guid workspaceId, Guid environmentId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<IReadOnlyList<SettingValueDto>> ListOrganisationSettingsAsync(Guid organisationId, CancellationToken ct = default);
    Task<SettingValueDto> UpsertOrganisationSettingAsync(Guid organisationId, string settingKey, UpsertSettingRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task DeleteOrganisationSettingAsync(Guid organisationId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigurationChangeDto>> GetChangeHistoryAsync(Guid workspaceId, Guid? environmentId, int take = 100, CancellationToken ct = default);
    Task<ConfigurationExportDto> ExportAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigurationChangeDto>> ImportAsync(Guid workspaceId, Guid environmentId, ImportConfigurationRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<SettingsValidationResultDto> ValidateEnvironmentSettingsAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default);
}

/// <summary>
/// Validates the effective AI provider configuration for an environment by
/// performing a real (but cost-free) call against the configured provider.
/// Secret values are resolved internally and never surfaced to callers.
/// </summary>
public interface IProviderValidationService
{
    Task<ProviderValidationResultDto> ValidateAsync(Guid workspaceId, Guid environmentId, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
}

public interface ISecretReferenceService
{
    Task<IReadOnlyList<SecretReferenceDto>> ListAsync(Guid workspaceId, CancellationToken ct = default);
    Task<SecretReferenceDto> GetAsync(Guid workspaceId, Guid referenceId, CancellationToken ct = default);
    Task<SecretReferenceDto> CreateAsync(Guid workspaceId, CreateSecretReferenceRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<SecretReferenceDto> UpdateAsync(Guid workspaceId, Guid referenceId, UpdateSecretReferenceRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<SecretReferenceDto> ValidateAsync(Guid workspaceId, Guid referenceId, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
    Task<SecretReferenceDto> DisableAsync(Guid workspaceId, Guid referenceId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default);
}

public interface IEffectiveConfigurationResolver
{
    Task<IReadOnlyList<EffectiveSettingResult>> ResolveAsync(Guid organisationId, Guid workspaceId, Guid? environmentId, CancellationToken ct = default);
    Task<EffectiveSettingResult?> ResolveOneAsync(Guid organisationId, Guid workspaceId, Guid? environmentId, string key, CancellationToken ct = default);
    Task<ConfigurationSnapshot> CreateSnapshotAsync(
        Guid organisationId,
        Guid workspaceId,
        Guid environmentId,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? executionOverrides = null);
}

public interface ISecretStore
{
    string? Resolve(string reference);
    bool Validate(string reference);
}
