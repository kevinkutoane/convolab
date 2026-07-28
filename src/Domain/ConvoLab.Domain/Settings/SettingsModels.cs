namespace ConvoLab.Domain.Settings;

// ─── Enumerations ────────────────────────────────────────────────────────────

public enum EnvironmentType { Development, Test, Staging, Production }
public enum EnvironmentStatus { Active, Suspended, Archived }
public enum SettingScope { Platform, Organisation, Workspace, Environment }
public enum SettingValueType { String, Boolean, Integer, Decimal, Percentage, Currency, Duration, Enum, Json, SecretReference }
public enum EvaluationFailureAction { Allow, Warn, Review, Block }
public enum SecretReferenceStatus { NotValidated, Valid, Missing, Invalid, Unavailable }

// ─── RuntimeEnvironment ──────────────────────────────────────────────────────

public sealed class RuntimeEnvironment
{
    public Guid Id { get; }
    public Guid OrganisationId { get; }
    public Guid WorkspaceId { get; }
    public string Name { get; private set; }
    public string Slug { get; }
    public EnvironmentType EnvironmentType { get; private set; }
    public string Description { get; private set; }
    public EnvironmentStatus Status { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public Guid CreatedBy { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public long Revision { get; private set; }

    public RuntimeEnvironment(
        Guid id, Guid organisationId, Guid workspaceId,
        string name, string slug, EnvironmentType type, string description,
        bool isDefault, Guid createdBy,
        DateTimeOffset createdAt, EnvironmentStatus status = EnvironmentStatus.Active, long revision = 1)
    {
        if (id == Guid.Empty) throw new ArgumentException("Environment id is required.");
        if (organisationId == Guid.Empty) throw new ArgumentException("Organisation id is required.");
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace id is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Environment name is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Environment slug is required.");

        Id = id; OrganisationId = organisationId; WorkspaceId = workspaceId;
        Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant();
        EnvironmentType = type; Description = description?.Trim() ?? "";
        Status = status; IsDefault = isDefault; CreatedAt = createdAt; CreatedBy = createdBy;
        UpdatedAt = createdAt; Revision = revision;
    }

    public void Update(string name, string description, EnvironmentType type, Guid updatedBy)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Environment name is required.");
        Name = name.Trim(); Description = description?.Trim() ?? ""; EnvironmentType = type;
        UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void Activate(Guid updatedBy)
    {
        if (Status == EnvironmentStatus.Archived) throw new InvalidOperationException("Archived environments cannot be activated.");
        Status = EnvironmentStatus.Active; UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void Suspend(bool isLastActiveProduction, bool isAdmin)
    {
        EnsureMutable();
        if (EnvironmentType == EnvironmentType.Production && isLastActiveProduction && !isAdmin)
            throw new InvalidOperationException("The final active Production environment cannot be suspended without Administrator access.");
        Status = EnvironmentStatus.Suspended; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void Archive()
    {
        EnsureMutable();
        if (IsDefault) throw new InvalidOperationException("The default environment cannot be archived. Change the default environment first.");
        Status = EnvironmentStatus.Archived; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void MakeDefault(Guid updatedBy)
    {
        if (Status != EnvironmentStatus.Active)
            throw new InvalidOperationException("Only active environments can be set as default.");
        IsDefault = true; UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void UnsetDefault()
    {
        IsDefault = false; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    private void EnsureMutable()
    {
        if (Status == EnvironmentStatus.Archived) throw new InvalidOperationException("Archived environments are immutable.");
    }
}

// ─── SettingDefinition ───────────────────────────────────────────────────────

public sealed class SettingDefinition
{
    public string Key { get; }
    public string Category { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public SettingValueType ValueType { get; }
    public string? DefaultValue { get; }
    public bool IsSecret { get; }
    public bool IsRequired { get; }
    public bool AllowsOrganisationOverride { get; }
    public bool AllowsWorkspaceOverride { get; }
    public bool AllowsEnvironmentOverride { get; }
    public string? ValidationRules { get; }
    public bool RequiresRestart { get; }
    public string? AllowedValues { get; }

    public SettingDefinition(
        string key, string category, string displayName, string description,
        SettingValueType valueType, string? defaultValue = null,
        bool isSecret = false, bool isRequired = false,
        bool allowsOrgOverride = true, bool allowsWorkspaceOverride = true, bool allowsEnvironmentOverride = true,
        string? validationRules = null, bool requiresRestart = false, string? allowedValues = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Setting key is required.");
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Setting category is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Setting display name is required.");

        Key = key.Trim(); Category = category.Trim(); DisplayName = displayName.Trim(); Description = description?.Trim() ?? "";
        ValueType = valueType; DefaultValue = defaultValue; IsSecret = isSecret; IsRequired = isRequired;
        AllowsOrganisationOverride = allowsOrgOverride; AllowsWorkspaceOverride = allowsWorkspaceOverride;
        AllowsEnvironmentOverride = allowsEnvironmentOverride; ValidationRules = validationRules;
        RequiresRestart = requiresRestart; AllowedValues = allowedValues;
    }
}

// ─── SettingValue ────────────────────────────────────────────────────────────

public sealed class SettingValue
{
    public Guid Id { get; }
    public string DefinitionKey { get; }
    public SettingScope Scope { get; }
    public Guid? OrganisationId { get; }
    public Guid? WorkspaceId { get; }
    public Guid? EnvironmentId { get; }
    public string ValueJson { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public Guid CreatedBy { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long Revision { get; private set; }

    public SettingValue(
        Guid id, string definitionKey, SettingScope scope,
        Guid? organisationId, Guid? workspaceId, Guid? environmentId,
        string valueJson, Guid createdBy, DateTimeOffset createdAt, long revision = 1)
    {
        if (id == Guid.Empty) throw new ArgumentException("Setting value id is required.");
        if (string.IsNullOrWhiteSpace(definitionKey)) throw new ArgumentException("Definition key is required.");
        if (string.IsNullOrWhiteSpace(valueJson)) throw new ArgumentException("Value is required.");

        Id = id; DefinitionKey = definitionKey; Scope = scope;
        OrganisationId = organisationId; WorkspaceId = workspaceId; EnvironmentId = environmentId;
        ValueJson = valueJson; CreatedBy = createdBy; UpdatedBy = createdBy; CreatedAt = createdAt; UpdatedAt = createdAt; Revision = revision;
    }

    public void Update(string valueJson, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(valueJson)) throw new ArgumentException("Value is required.");
        ValueJson = valueJson; UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }
}

// ─── SecretReference ─────────────────────────────────────────────────────────

public sealed class SecretReference
{
    public Guid Id { get; }
    public Guid WorkspaceId { get; }
    public string DisplayName { get; private set; }
    public string Reference { get; private set; }
    public string Provider { get; private set; }
    public SecretReferenceStatus Status { get; private set; }
    public DateTimeOffset? LastValidatedAt { get; private set; }
    public string? LastValidationOutcome { get; private set; }
    public bool IsDisabled { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public Guid CreatedBy { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public long Revision { get; private set; }

    public SecretReference(
        Guid id, Guid workspaceId, string displayName, string reference,
        Guid createdBy, DateTimeOffset createdAt, long revision = 1)
    {
        if (id == Guid.Empty) throw new ArgumentException("Secret reference id is required.");
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.");
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Reference is required.");

        var (provider, _) = ParseReference(reference);

        Id = id; WorkspaceId = workspaceId; DisplayName = displayName.Trim();
        Reference = reference.Trim(); Provider = provider;
        Status = SecretReferenceStatus.NotValidated; CreatedAt = createdAt; CreatedBy = createdBy;
        UpdatedAt = createdAt; Revision = revision;
    }

    public void Update(string displayName, string reference, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.");
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Reference is required.");
        var (provider, _) = ParseReference(reference);
        DisplayName = displayName.Trim(); Reference = reference.Trim(); Provider = provider;
        Status = SecretReferenceStatus.NotValidated;
        UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void RecordValidation(bool success, string outcome, Guid updatedBy)
    {
        Status = success ? SecretReferenceStatus.Valid : SecretReferenceStatus.Invalid;
        LastValidatedAt = DateTimeOffset.UtcNow; LastValidationOutcome = outcome;
        UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void MarkMissing()
    {
        Status = SecretReferenceStatus.Missing; LastValidatedAt = DateTimeOffset.UtcNow;
        LastValidationOutcome = "Secret not found at reference location.";
        UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public void Disable(Guid updatedBy)
    {
        IsDisabled = true; UpdatedBy = updatedBy; UpdatedAt = DateTimeOffset.UtcNow; Revision++;
    }

    public static (string provider, string key) ParseReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.");

        var colonIndex = reference.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex <= 0)
            throw new ArgumentException($"Invalid secret reference format '{reference}'. Expected 'provider:key'.");

        var provider = reference[..colonIndex].Trim().ToLowerInvariant();
        var key = reference[(colonIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException($"Secret reference key is empty in '{reference}'.");

        return (provider, key);
    }
}

// ─── ConfigurationChange ─────────────────────────────────────────────────────

public sealed class ConfigurationChange
{
    public Guid Id { get; }
    public Guid OrganisationId { get; }
    public Guid? WorkspaceId { get; }
    public Guid? EnvironmentId { get; }
    public string SettingKey { get; }
    public string? PreviousValueSummary { get; }
    public string NewValueSummary { get; }
    public Guid ChangedBy { get; }
    public string ChangedByDisplay { get; }
    public DateTimeOffset ChangedAt { get; }
    public string? Reason { get; }
    public string CorrelationId { get; }
    public string Outcome { get; }
    public long Revision { get; }

    public ConfigurationChange(
        Guid id, Guid organisationId, Guid? workspaceId, Guid? environmentId,
        string settingKey, string? previousValueSummary, string newValueSummary,
        Guid changedBy, string changedByDisplay, DateTimeOffset changedAt,
        string? reason, string correlationId, string outcome, long revision = 1)
    {
        if (id == Guid.Empty) throw new ArgumentException("Change id is required.");
        if (string.IsNullOrWhiteSpace(settingKey)) throw new ArgumentException("Setting key is required.");

        Id = id; OrganisationId = organisationId; WorkspaceId = workspaceId; EnvironmentId = environmentId;
        SettingKey = settingKey; PreviousValueSummary = previousValueSummary; NewValueSummary = newValueSummary;
        ChangedBy = changedBy; ChangedByDisplay = changedByDisplay; ChangedAt = changedAt;
        Reason = reason; CorrelationId = correlationId; Outcome = outcome; Revision = revision;
    }
}

// ─── Platform setting keys ────────────────────────────────────────────────────

public static class SettingKeys
{
    // General
    public const string DefaultLocale = "general.locale";
    public const string DefaultTimezone = "general.timezone";
    public const string DefaultCurrency = "general.currency";

    // AI Provider
    public const string AiProvider = "ai.provider";
    public const string AiModel = "ai.model";
    public const string AiSecretReference = "ai.secret_reference";
    public const string AiRequestTimeoutSeconds = "ai.request_timeout_seconds";
    public const string AiMaxRetryCount = "ai.max_retry_count";
    public const string AiTemperature = "ai.temperature";
    public const string AiMaxOutputTokens = "ai.max_output_tokens";
    public const string AiProviderEnabled = "ai.provider_enabled";

    // Budget
    public const string MonthlyBudgetZar = "budget.monthly_zar";
    public const string BudgetWarningThreshold = "budget.warning_threshold";
    public const string BudgetHardStopThreshold = "budget.hard_stop_threshold";
    public const string AiInputPriceZarPer1K = "budget.input_price_zar_per_1k";
    public const string AiOutputPriceZarPer1K = "budget.output_price_zar_per_1k";
    public const string AllowExecutionWhenPricingUnknown = "budget.allow_unknown_pricing";

    // Evaluation
    public const string EvalMinGroundedness = "evaluation.min_groundedness";
    public const string EvalMinRelevance = "evaluation.min_relevance";
    public const string EvalMinSafety = "evaluation.min_safety";
    public const string EvalMinOverall = "evaluation.min_overall";
    public const string EvalFailureAction = "evaluation.failure_action";

    // Trace & Retention
    public const string TraceRetentionDays = "retention.trace_days";
    public const string EvalRetentionDays = "retention.evaluation_days";
    public const string ReplayRetentionDays = "retention.replay_days";
    public const string AnalyticsEventRetentionDays = "retention.analytics_event_days";
    public const string AnalyticsHourlyRetentionDays = "retention.analytics_hourly_days";
    public const string AnalyticsDailyRetentionDays = "retention.analytics_daily_days";
    public const string AnalyticsExportRetentionDays = "retention.analytics_export_days";
    public const string StoreProviderPayloads = "retention.store_provider_payloads";
    public const string StoreProviderResponses = "retention.store_provider_responses";
    public const string DefaultRedactionLevel = "retention.redaction_level";
    public const string AllowSensitiveArtifactReveal = "retention.allow_sensitive_reveal";

    // Feature flags
    public const string FeatureProviderExecution = "feature.provider_execution";
    public const string FeatureReplayExecution = "feature.replay_execution";
    public const string FeaturePluginActivation = "feature.plugin_activation";
    public const string FeaturePolicyEnforcement = "feature.policy_enforcement";
    public const string FeatureExperimental = "feature.experimental";
    public const string FeatureSensitiveTraceReveal = "feature.sensitive_trace_reveal";

    // Plugin defaults
    public const string PluginAllowWorkspaceRegistration = "plugin.allow_workspace_registration";
    public const string PluginAllowManifestUrl = "plugin.allow_manifest_url";
    public const string PluginRequireHealthy = "plugin.require_healthy";
    public const string PluginRequireCompatibility = "plugin.require_compatibility";
    public const string PluginAllowPlatform = "plugin.allow_platform";

    // Policy defaults
    public const string PolicyEnforcementEnabled = "policy.enforcement_enabled";
    public const string PolicyDefaultDenialBehaviour = "policy.default_denial_behaviour";
    public const string PolicyRequireBeforeProvider = "policy.require_before_provider";
    public const string PolicyAuditAll = "policy.audit_all";
}

// ─── Settings permissions ─────────────────────────────────────────────────────

public static class SettingsPermissions
{
    public const string ViewSettings = "CanViewSettings";
    public const string ManageWorkspaceSettings = "CanManageWorkspaceSettings";
    public const string ManageEnvironmentSettings = "CanManageEnvironmentSettings";
    public const string ManageOrganisationSettings = "CanManageOrganisationSettings";
    public const string ManageSecretReferences = "CanManageSecretReferences";
    public const string ValidateProviderConfiguration = "CanValidateProviderConfiguration";
    public const string ExportConfiguration = "CanExportConfiguration";
    public const string ImportConfiguration = "CanImportConfiguration";
    public const string ManageFeatureFlags = "CanManageFeatureFlags";
}

// ─── Results ──────────────────────────────────────────────────────────────────

public sealed record EffectiveSettingResult(
    string Key,
    string? EffectiveValue,
    SettingValueType ValueType,
    SettingScope SourceScope,
    Guid? SourceId,
    bool IsInherited,
    bool IsSecret,
    string ValidationStatus,
    bool RequiresRestart,
    string DisplayName,
    string Category,
    string? InheritedFromDisplay);

public sealed record ConfigurationSnapshot(
    string ConfigurationRevision,
    Guid EnvironmentId,
    string EnvironmentName,
    string? Provider,
    string? Model,
    decimal? MonthlyBudgetZar,
    decimal? EvalMinGroundedness,
    decimal? EvalMinRelevance,
    decimal? EvalMinSafety,
    decimal? EvalMinOverall,
    string? EvalFailureAction,
    bool PolicyEnforcementEnabled,
    bool ProviderExecutionEnabled,
    IReadOnlyDictionary<string, string?> FeatureFlags,
    DateTimeOffset CreatedAt);
