using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Operations;
using ConvoLab.Domain.Analytics;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations;

public sealed class PlatformOperationalStateService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PlatformOperationalStateService> logger)
    : IPlatformOperationalState, IPlatformOperationalAdministration
{
    public async Task<PlatformOperationalState> GetAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = await db.PlatformOperationalSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == "platform", ct);
        var overrideEnabled = string.Equals(
            Environment.GetEnvironmentVariable("CONVOLAB_SAFE_MODE"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var persisted = record?.SafeModeEnabled ?? false;
        return new(
            persisted,
            overrideEnabled,
            persisted || overrideEnabled,
            configuration.GetValue("SafeMode:AllowDeterministicVerification", false),
            configuration.GetValue<bool?>("SafeMode:BlockAnalyticsExports"),
            record?.SafeModeReason,
            record?.Revision ?? 1,
            record?.ChangedAt ?? DateTimeOffset.MinValue);
    }

    public async Task EnsureExternalExecutionAllowedAsync(CancellationToken ct = default)
    {
        if (!(await GetAsync(ct)).EffectiveSafeModeEnabled) return;
        ConvoLabTelemetry.SafeModeBlocks.Add(1, Tags("external"));
        throw Blocked();
    }

    public async Task EnsureDeterministicExecutionAllowedAsync(CancellationToken ct = default)
    {
        var state = await GetAsync(ct);
        if (!state.EffectiveSafeModeEnabled || state.AllowDeterministicVerification) return;
        ConvoLabTelemetry.SafeModeBlocks.Add(1, Tags("deterministic"));
        throw Blocked();
    }

    public async Task EnsureAnalyticsExportAllowedAsync(CancellationToken ct = default)
    {
        var state = await GetAsync(ct);
        if (!state.EffectiveSafeModeEnabled || state.BlockAnalyticsExports != true) return;
        ConvoLabTelemetry.SafeModeBlocks.Add(1, Tags("analytics_export"));
        throw Blocked();
    }

    public async Task<PlatformOperationalState> UpdateSafeModeAsync(
        UpdateSafeModeCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length < 8)
            throw new RequestValidationException(
                "safe_mode.reason_required", "A reason of at least eight characters is required.");
        var expectedConfirmation = command.Enabled
            ? "ACTIVATE SAFE MODE"
            : "DEACTIVATE SAFE MODE";
        if (!string.Equals(command.Confirmation, expectedConfirmation, StringComparison.Ordinal))
            throw new RequestValidationException(
                "safe_mode.confirmation_invalid", $"Type '{expectedConfirmation}' to confirm this action.");
        if (!command.Enabled && string.Equals(
                Environment.GetEnvironmentVariable("CONVOLAB_SAFE_MODE"), "true",
                StringComparison.OrdinalIgnoreCase))
            throw new ResourceConflictException(
                "safe_mode.override_active", "The environment safe-mode override must be removed before deactivation.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var record = await db.PlatformOperationalSettings.SingleAsync(item => item.Key == "platform", ct);
        if (record.Revision != command.ExpectedRevision)
            throw new ResourceConflictException(
                "revision.conflict", "The operational setting changed. Refresh and retry.");

        var now = DateTimeOffset.UtcNow;
        record.SafeModeEnabled = command.Enabled;
        record.SafeModeReason = command.Reason.Trim();
        record.ChangedBy = command.ActorId;
        record.ChangedAt = now;
        record.Revision++;
        var evidenceScope = command.WorkspaceId.HasValue && command.OrganisationId.HasValue
            ? null
            : await db.RuntimeEnvironments.AsNoTracking()
                .Where(item => item.IsDefault && item.Status == "Active")
                .OrderBy(item => item.Id)
                .Select(item => new { item.OrganisationId, item.WorkspaceId, EnvironmentId = item.Id })
                .FirstOrDefaultAsync(ct);
        var organisationId = command.OrganisationId ?? evidenceScope?.OrganisationId;
        var workspaceId = command.WorkspaceId ?? evidenceScope?.WorkspaceId;
        var audit = new AuditEventRecord
        {
            Id = Guid.NewGuid(),
            Scope = "Platform",
            OrganisationId = organisationId,
            WorkspaceId = workspaceId,
            ActorType = "User",
            ActorId = command.ActorId,
            ActorDisplay = command.ActorDisplay,
            Action = command.Enabled ? "SafeMode.Activated" : "SafeMode.Deactivated",
            ResourceType = "PlatformOperationalSettings",
            ResourceId = "platform",
            Outcome = "Succeeded",
            DetailJson = "{}",
            CorrelationId = command.CorrelationId,
            OccurredAt = now
        };
        db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(
            db, audit, evidenceScope?.EnvironmentId, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogWarning(
            "Safe mode changed {SafeModeEnabled} by an authorized platform administrator",
            command.Enabled);
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("safe_mode.change");
        activity?.SetTag("safe_mode.enabled", command.Enabled);
        System.Diagnostics.TagList tags = default;
        tags.Add("enabled", command.Enabled);
        ConvoLabTelemetry.SafeModeChanges.Add(1, tags);
        return await GetAsync(ct);
    }

    private static CapabilityUnavailableException Blocked() => new(
        "operations.safe_mode_active",
        "The operation is unavailable while platform safe mode is active.");

    private static System.Diagnostics.TagList Tags(string operation)
    {
        System.Diagnostics.TagList tags = default;
        tags.Add("operation", operation);
        return tags;
    }
}
