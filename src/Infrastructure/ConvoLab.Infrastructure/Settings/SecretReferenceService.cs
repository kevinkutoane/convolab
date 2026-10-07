using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Analytics;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Settings;

public sealed class SecretReferenceService : ISecretReferenceService
{
    private readonly ApplicationDbContext _db;
    private readonly ISecretStore _secretStore;

    private const string ResourceName = "secret_reference";

    public SecretReferenceService(ApplicationDbContext db, ISecretStore secretStore)
    {
        _db = db; _secretStore = secretStore;
    }

    public async Task<IReadOnlyList<SecretReferenceDto>> ListAsync(Guid workspaceId, CancellationToken ct = default) =>
        (await _db.SecretReferences.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderBy(r => r.DisplayName)
            .ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<SecretReferenceDto> GetAsync(Guid workspaceId, Guid referenceId, CancellationToken ct = default)
    {
        var record = await _db.SecretReferences.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == referenceId && r.WorkspaceId == workspaceId, ct)
            ?? throw NotFound(ResourceName, referenceId);
        return ToDto(record);
    }

    public async Task<SecretReferenceDto> CreateAsync(Guid workspaceId, CreateSecretReferenceRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Reference))
            throw new RequestValidationException("secret_reference.invalid", "Display name and reference are required.");

        var (provider, _) = ValidateAndParseReference(request.Reference);

        var now = DateTimeOffset.UtcNow;
        var record = new SecretReferenceRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            DisplayName = request.DisplayName.Trim(),
            Reference = request.Reference.Trim(),
            Provider = provider,
            Status = "NotValidated",
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            Revision = 1
        };
        _db.SecretReferences.Add(record);
        await AddAuditAsync(workspaceId, "SecretReference.Created", record.Id, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(record);
    }

    public async Task<SecretReferenceDto> UpdateAsync(Guid workspaceId, Guid referenceId, UpdateSecretReferenceRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await _db.SecretReferences.SingleOrDefaultAsync(r => r.Id == referenceId && r.WorkspaceId == workspaceId, ct)
            ?? throw NotFound(ResourceName, referenceId);
        if (record.Revision != request.ExpectedRevision) throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry.");

        var (provider, _) = ValidateAndParseReference(request.Reference);
        var previousReference = record.Reference;
        record.DisplayName = request.DisplayName.Trim();
        record.Reference = request.Reference.Trim();
        record.Provider = provider;
        record.Status = "NotValidated";
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.UpdatedBy = actorId;
        record.Revision++;

        await AddAuditAsync(workspaceId, "SecretReference.Updated", record.Id, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
        _secretStore.Invalidate(previousReference);
        _secretStore.Invalidate(record.Reference);
        return ToDto(record);
    }

    public async Task<SecretReferenceDto> ValidateAsync(Guid workspaceId, Guid referenceId, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await _db.SecretReferences.SingleOrDefaultAsync(r => r.Id == referenceId && r.WorkspaceId == workspaceId, ct)
            ?? throw NotFound(ResourceName, referenceId);

        var validation = await _secretStore.ValidateAsync(record.Reference, ct);
        var resolved = validation.IsValid;
        record.Status = resolved ? "Valid" : validation.Status.ToString();
        record.LastValidatedAt = DateTimeOffset.UtcNow;
        record.LastValidationOutcome = resolved ? "Secret resolved successfully." : "Secret not found at reference location.";
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.UpdatedBy = actorId;
        record.Revision++;

        await AddAuditAsync(workspaceId, "SecretReference.Validated", record.Id, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
        _secretStore.Invalidate(record.Reference);
        return ToDto(record);
    }

    public async Task<SecretReferenceDto> DisableAsync(Guid workspaceId, Guid referenceId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await _db.SecretReferences.SingleOrDefaultAsync(r => r.Id == referenceId && r.WorkspaceId == workspaceId, ct)
            ?? throw NotFound(ResourceName, referenceId);
        if (record.Revision != expectedRevision) throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry.");

        record.IsDisabled = true; record.UpdatedAt = DateTimeOffset.UtcNow; record.UpdatedBy = actorId; record.Revision++;
        await AddAuditAsync(workspaceId, "SecretReference.Disabled", record.Id, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
        _secretStore.Invalidate(record.Reference);
        return ToDto(record);
    }

    private async Task AddAuditAsync(
        Guid workspaceId,
        string action,
        Guid resourceId,
        Guid actorId,
        string actorDisplay,
        string correlationId,
        CancellationToken ct)
    {
        var organisationId = await _db.Workspaces.AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => (Guid?)item.OrganisationId)
            .SingleOrDefaultAsync(ct);
        var audit = new WorkspaceIdentity.AuditEventRecord
        {
            Id = Guid.NewGuid(), Scope = "Workspace", OrganisationId = organisationId,
            WorkspaceId = workspaceId,
            ActorType = "User", ActorId = actorId, ActorDisplay = actorDisplay,
            Action = action, ResourceType = "SecretReference", ResourceId = resourceId.ToString(),
            Outcome = "Succeeded", DetailJson = "{}", CorrelationId = correlationId, OccurredAt = DateTimeOffset.UtcNow
        };
        _db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(_db, audit, cancellationToken: ct);
    }

    private static SecretReferenceDto ToDto(SecretReferenceRecord r) =>
        new(r.Id, r.WorkspaceId, r.DisplayName, r.Reference, r.Provider,
            r.Status, r.LastValidatedAt, r.LastValidationOutcome,
            r.IsDisabled, r.CreatedAt, r.UpdatedAt, r.Revision);

    private static (string provider, string key) ValidateAndParseReference(string reference)
    {
        try
        {
            return SecretReference.ParseReference(reference);
        }
        catch (ArgumentException)
        {
            throw new RequestValidationException(
                "secret_reference.invalid_format",
                $"Secret reference '{reference}' is invalid. References must follow the format 'provider:key' (such as 'env:GEMINI_API_KEY', 'docker-secret:...', or 'azure:...'). Raw API keys must be set in your host environment (.env file) or secrets vault, not submitted to the platform.");
        }
    }

    private static ResourceNotFoundException NotFound(string resource, Guid id) =>
        new($"{resource}.not_found", $"{resource} '{id}' was not found.");
}
