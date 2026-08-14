using System.Text.Json;
using ConvoLab.Domain.Analytics;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Analytics;

public static class AnalyticsOutboxFactory
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static async Task EnqueueAuditAsync(
        ApplicationDbContext db,
        AuditEventRecord audit,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!audit.WorkspaceId.HasValue || !audit.OrganisationId.HasValue) return;
        environmentId ??= await db.RuntimeEnvironments.AsNoTracking()
            .Where(item => item.WorkspaceId == audit.WorkspaceId
                && item.IsDefault
                && item.Status == "Active")
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!environmentId.HasValue) return;

        var capability = audit.Action.Split(
            '.',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        Guid? sourceId = Guid.TryParse(audit.ResourceId, out var parsedSourceId)
            ? parsedSourceId
            : null;
        var eventType = AuditEventType(audit);
        Enqueue(db, new AnalyticsEventRecord
        {
            Id = Guid.NewGuid(),
            EventKey = AnalyticsKeys.Event("WorkspaceAudit", audit.Id, eventType),
            OrganisationId = audit.OrganisationId.Value,
            WorkspaceId = audit.WorkspaceId.Value,
            EnvironmentId = environmentId.Value,
            ActorId = audit.ActorId,
            ActorType = audit.ActorType,
            Capability = capability,
            EventType = eventType,
            Outcome = audit.Outcome,
            CostType = "Unavailable",
            SourceType = audit.ResourceType,
            SourceId = sourceId,
            ConfigurationRevision = "not-applicable",
            CorrelationId = audit.CorrelationId,
            OccurredAt = audit.OccurredAt
        });
    }

    private static string AuditEventType(AuditEventRecord audit) =>
        audit.Action switch
        {
            "Authentication.Login" when audit.Outcome == "Succeeded" => "UserLoggedIn",
            "Authentication.Login" => "UserLoginFailed",
            "Authentication.Logout" => "UserLoggedOut",
            "Authentication.EntraLogin" when audit.Outcome == "Succeeded" => "UserLoggedIn",
            "Authentication.EntraLogin" => "UserLoginFailed",
            "Authentication.ExternalIdentityLinked" => "ExternalIdentityLinked",
            "Authentication.ExternalIdentityDisabled" => "ExternalIdentityDisabled",
            "Authentication.ExternalIdentityEnabled" => "ExternalIdentityEnabled",
            "Authentication.ExternalIdentityRemoved" => "ExternalIdentityRemoved",
            "Authentication.BreakGlassLogin" => "BreakGlassLogin",
            "Workspace.Selected" => "WorkspaceSelected",
            "Trace.SensitiveContentRevealed" => "SensitiveTraceRevealed",
            "Plugin.Activated" => "PluginActivated",
            "Plugin.Deactivated" => "PluginDeactivated",
            "Plugin.ActivationFailed" => "PluginActivationFailed",
            "Plugin.HealthChecked" => "PluginHealthChecked",
            "Plugin.CompatibilityFailed" => "PluginCompatibilityFailed",
            "Configuration.Imported" => "ConfigurationImported",
            "Configuration.Exported" => "ConfigurationExported",
            "Configuration.ProviderValidated" => "ProviderConfigurationValidated",
            "Configuration.SecretReferenceValidated" or "SecretReference.Validated" => "SecretReferenceValidated",
            _ => audit.Action
        };

    public static async Task EnqueueConfigurationChangeAsync(
        ApplicationDbContext db,
        ConfigurationChangeRecord change,
        CancellationToken cancellationToken = default)
    {
        var environmentId = change.EnvironmentId
            ?? await db.RuntimeEnvironments.AsNoTracking()
                .Where(item => item.WorkspaceId == change.WorkspaceId
                    && item.IsDefault
                    && item.Status == "Active")
                .Select(item => (Guid?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);
        if (!change.WorkspaceId.HasValue || !environmentId.HasValue) return;
        var eventType = change.SettingKey switch
        {
            "ai.provider_validation" => "ProviderConfigurationValidated",
            "configuration.import" => "ConfigurationImported",
            "configuration.export" => "ConfigurationExported",
            _ => await db.RuntimeEnvironments.AsNoTracking()
                .Where(item => item.Id == environmentId.Value)
                .Select(item => item.EnvironmentType)
                .SingleOrDefaultAsync(cancellationToken) == "Production"
                    ? "ProductionConfigurationChanged"
                    : "ConfigurationChanged"
        };

        Enqueue(db, new AnalyticsEventRecord
        {
            Id = Guid.NewGuid(),
            EventKey = AnalyticsKeys.Event(
                "ConfigurationChange",
                change.Id,
                eventType),
            OrganisationId = change.OrganisationId,
            WorkspaceId = change.WorkspaceId.Value,
            EnvironmentId = environmentId.Value,
            ActorId = change.ChangedBy,
            ActorType = "User",
            Capability = "Configuration",
            EventType = eventType,
            Outcome = change.Outcome,
            CostType = "Unavailable",
            SourceType = "ConfigurationChange",
            SourceId = change.Id,
            ConfigurationRevision = "pending-effective-snapshot",
            CorrelationId = change.CorrelationId,
            OccurredAt = change.ChangedAt
        });
    }

    public static void Enqueue(
        ApplicationDbContext db,
        AnalyticsEventRecord analyticsEvent)
    {
        if (analyticsEvent.CostZar.HasValue
            && analyticsEvent.CostType is "Actual" or "Estimated")
        {
            System.Diagnostics.TagList tags = default;
            tags.Add("provider_type", ProviderType(analyticsEvent.Provider));
            tags.Add("cost_type", analyticsEvent.CostType.ToLowerInvariant());
            tags.Add("outcome", BoundedOutcome(analyticsEvent.Outcome));
            ConvoLabTelemetry.ProviderCostZar.Add(
                decimal.ToDouble(analyticsEvent.CostZar.Value),
                tags);
        }
        db.AnalyticsOutbox.Add(new AnalyticsOutboxRecord
        {
            Id = Guid.NewGuid(),
            EventKey = analyticsEvent.EventKey,
            PayloadJson = JsonSerializer.Serialize(analyticsEvent, JsonOptions),
            Status = "Pending",
            AvailableAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string ProviderType(string? provider) => provider switch
    {
        null or "" => "none",
        var value when value.Contains("deterministic", StringComparison.OrdinalIgnoreCase)
            => "deterministic",
        _ => "external"
    };

    private static string BoundedOutcome(string outcome) => outcome switch
    {
        "Succeeded" or "Completed" => "succeeded",
        "Denied" => "denied",
        "Failed" => "failed",
        _ => "other"
    };
}
