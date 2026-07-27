using ConvoLab.Domain.Settings;

namespace ConvoLab.Infrastructure.Settings;

public sealed class RuntimeEnvironmentRecord
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string EnvironmentType { get; set; } = "Development";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class SettingDefinitionRecord
{
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ValueType { get; set; } = "String";
    public string? DefaultValue { get; set; }
    public bool IsSecret { get; set; }
    public bool IsRequired { get; set; }
    public bool AllowsOrganisationOverride { get; set; } = true;
    public bool AllowsWorkspaceOverride { get; set; } = true;
    public bool AllowsEnvironmentOverride { get; set; } = true;
    public string? ValidationRules { get; set; }
    public bool RequiresRestart { get; set; }
    public string? AllowedValues { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SettingValueRecord
{
    public Guid Id { get; set; }
    public string DefinitionKey { get; set; } = string.Empty;
    public string Scope { get; set; } = "Workspace";
    public Guid? OrganisationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public string ValueJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class SecretReferenceRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = "NotValidated";
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? LastValidationOutcome { get; set; }
    public bool IsDisabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class ConfigurationChangeRecord
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string? PreviousValueSummary { get; set; }
    public string NewValueSummary { get; set; } = string.Empty;
    public Guid ChangedBy { get; set; }
    public string ChangedByDisplay { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Outcome { get; set; } = "Succeeded";
    public long Revision { get; set; } = 1;
}

// ─── Mappers ──────────────────────────────────────────────────────────────────

public static class SettingsRecordMapper
{
    public static RuntimeEnvironment ToDomain(this RuntimeEnvironmentRecord r) =>
        new(r.Id, r.OrganisationId, r.WorkspaceId, r.Name, r.Slug,
            Enum.Parse<EnvironmentType>(r.EnvironmentType), r.Description,
            r.IsDefault, r.CreatedBy, r.CreatedAt,
            Enum.Parse<EnvironmentStatus>(r.Status), r.Revision);

    public static RuntimeEnvironmentRecord ToRecord(this RuntimeEnvironment e) => new()
    {
        Id = e.Id, OrganisationId = e.OrganisationId, WorkspaceId = e.WorkspaceId,
        Name = e.Name, Slug = e.Slug, EnvironmentType = e.EnvironmentType.ToString(),
        Description = e.Description, Status = e.Status.ToString(), IsDefault = e.IsDefault,
        CreatedAt = e.CreatedAt, CreatedBy = e.CreatedBy, UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy, Revision = e.Revision
    };

    public static SettingValue ToDomain(this SettingValueRecord r) =>
        new(r.Id, r.DefinitionKey, Enum.Parse<SettingScope>(r.Scope),
            r.OrganisationId, r.WorkspaceId, r.EnvironmentId,
            r.ValueJson, r.CreatedBy, r.CreatedAt, r.Revision);

    public static SecretReference ToDomain(this SecretReferenceRecord r) =>
        new(r.Id, r.WorkspaceId, r.DisplayName, r.Reference, r.CreatedBy, r.CreatedAt, r.Revision);
}
