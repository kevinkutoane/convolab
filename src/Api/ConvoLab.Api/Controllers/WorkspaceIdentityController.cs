using System.Security.Claims;
using System.Text.Json;
using ConvoLab.Api.Security;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/organisations")]
public sealed class OrganisationsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganisationRecord>>> List(CancellationToken ct)
    {
        var organisationId = ClaimGuid("organisation_id");
        var query = db.Organisations.AsNoTracking();
        if (!User.HasClaim("platform_administrator", "true")) query = query.Where(item => item.Id == organisationId);
        return Ok(await query.OrderBy(item => item.Name).ToListAsync(ct));
    }

    [Authorize(Policy = "PlatformAdministrator")]
    [HttpPost]
    public async Task<ActionResult<OrganisationRecord>> Create(CreateOrganisationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug)) throw Invalid("organisation.invalid", "Name and slug are required.");
        var now = DateTimeOffset.UtcNow;
        var item = new OrganisationRecord { Id = Guid.NewGuid(), Name = request.Name.Trim(), Slug = request.Slug.Trim().ToLowerInvariant(), Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now };
        db.Organisations.Add(item); Audit(db, "Platform", null, "Organisation.Created", "Organisation", item.Id, HttpContext.TraceIdentifier); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), item);
    }

    [Authorize(Policy = "PlatformAdministrator")]
    [HttpPost("{organisationId:guid}/{lifecycleAction:regex(^(activate|suspend|archive)$)}")]
    public async Task<ActionResult<OrganisationRecord>> Lifecycle(Guid organisationId, string lifecycleAction, RevisionRequest request, CancellationToken ct)
    {
        var item = await db.Organisations.SingleOrDefaultAsync(value => value.Id == organisationId, ct) ?? throw Missing("organisation", organisationId);
        EnsureRevision(item.Revision, request.ExpectedRevision);
        if (lifecycleAction == "archive" && await db.Workspaces.AnyAsync(workspace => workspace.OrganisationId == item.Id && workspace.Status == "Active", ct))
            throw new ResourceConflictException("organisation.active_workspaces", "Archive or suspend active workspaces first.");
        item.Status = lifecycleAction switch { "activate" => "Active", "suspend" => "Suspended", _ => "Archived" }; item.Revision++; item.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, "Platform", null, $"Organisation.{lifecycleAction}", "Organisation", item.Id, HttpContext.TraceIdentifier); await db.SaveChangesAsync(ct); return Ok(item);
    }

    private Guid? ClaimGuid(string type) => Guid.TryParse(User.FindFirstValue(type), out var id) ? id : null;
    internal static void EnsureRevision(long actual, long expected) { if (actual != expected) throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry."); }
    internal static ResourceNotFoundException Missing(string resource, Guid id) => new($"{resource}.not_found", $"{resource} '{id}' was not found.");
    internal static RequestValidationException Invalid(string code, string detail) => new(code, detail);
    internal static void Audit(ApplicationDbContext db, string scope, Guid? workspaceId, string action, string resourceType, Guid resourceId, string correlationId) => db.WorkspaceAuditEvents.Add(AuthController.Audit(scope, null, workspaceId, "User", null, "Authenticated user", action, resourceType, resourceId.ToString(), "Succeeded", correlationId));
}

[ApiController]
[Route("api/workspaces")]
public sealed class WorkspacesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceRecord>>> List(CancellationToken ct)
    {
        var userId = ClaimGuid(ClaimTypes.NameIdentifier);
        var workspaceIds = await db.WorkspaceMemberships.AsNoTracking().Where(item => item.UserId == userId && item.Status == "Active").Select(item => item.WorkspaceId).ToListAsync(ct);
        return Ok(await db.Workspaces.AsNoTracking().Where(item => workspaceIds.Contains(item.Id)).OrderBy(item => item.Name).ToListAsync(ct));
    }

    [Authorize(Policy = WorkspacePermissions.ManageWorkspace)]
    [HttpGet("{workspaceId:guid}")]
    public async Task<ActionResult<WorkspaceRecord>> Get(Guid workspaceId, CancellationToken ct)
        => Ok(await CurrentWorkspace(workspaceId, false, ct));

    [Authorize(Policy = WorkspacePermissions.ManageWorkspace)]
    [HttpPost]
    public async Task<ActionResult<WorkspaceRecord>> Create(CreateWorkspaceRequest request, CancellationToken ct)
    {
        var organisationId = ClaimGuid("organisation_id") ?? throw OrganisationsController.Invalid("workspace.organisation_required", "An active organisation is required.");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug)) throw OrganisationsController.Invalid("workspace.invalid", "Name and slug are required.");
        var now = DateTimeOffset.UtcNow; var userId = ClaimGuid(ClaimTypes.NameIdentifier)!.Value;
        var item = new WorkspaceRecord { Id = Guid.NewGuid(), OrganisationId = organisationId, Name = request.Name.Trim(), Slug = request.Slug.Trim().ToLowerInvariant(), Description = request.Description?.Trim() ?? "", Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now };
        db.Workspaces.Add(item); db.WorkspaceMemberships.Add(new WorkspaceMembershipRecord { Id = Guid.NewGuid(), WorkspaceId = item.Id, UserId = userId, Role = "Administrator", Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now });
        AddAudit(item.Id, "Workspace.Created", "Workspace", item.Id); await db.SaveChangesAsync(ct); return CreatedAtAction(nameof(Get), new { workspaceId = item.Id }, item);
    }

    [Authorize(Policy = WorkspacePermissions.ManageWorkspace)]
    [HttpPut("{workspaceId:guid}")]
    public async Task<ActionResult<WorkspaceRecord>> Update(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var item = await CurrentWorkspace(workspaceId, true, ct); OrganisationsController.EnsureRevision(item.Revision, request.ExpectedRevision);
        if (item.Status == "Archived") throw new ResourceConflictException("workspace.archived", "Archived workspaces are immutable.");
        item.Name = request.Name.Trim(); item.Description = request.Description?.Trim() ?? ""; item.Revision++; item.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(item.Id, "Workspace.Updated", "Workspace", item.Id); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [Authorize(Policy = WorkspacePermissions.ManageWorkspace)]
    [HttpPost("{workspaceId:guid}/{lifecycleAction:regex(^(activate|suspend|archive)$)}")]
    public async Task<ActionResult<WorkspaceRecord>> Lifecycle(Guid workspaceId, string lifecycleAction, RevisionRequest request, CancellationToken ct)
    {
        var item = await CurrentWorkspace(workspaceId, true, ct); OrganisationsController.EnsureRevision(item.Revision, request.ExpectedRevision);
        if (item.Status == "Archived" && lifecycleAction != "archive") throw new ResourceConflictException("workspace.archived", "Archived workspaces are immutable.");
        item.Status = lifecycleAction switch { "activate" => "Active", "suspend" => "Suspended", _ => "Archived" }; item.Revision++; item.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(item.Id, $"Workspace.{lifecycleAction}", "Workspace", item.Id); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [Authorize(Policy = WorkspacePermissions.ManageMembers)]
    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult> Members(Guid workspaceId, CancellationToken ct)
    {
        await CurrentWorkspace(workspaceId, false, ct);
        var rows = await (from membership in db.WorkspaceMemberships.AsNoTracking() join user in db.IdentityUsers.AsNoTracking() on membership.UserId equals user.Id where membership.WorkspaceId == workspaceId select new { membership.Id, membership.UserId, user.Email, user.DisplayName, membership.Role, membership.Status, membership.Revision, membership.CreatedAt }).ToListAsync(ct);
        return Ok(rows);
    }

    [Authorize(Policy = WorkspacePermissions.ManageMembers)]
    [HttpPost("{workspaceId:guid}/members")]
    public async Task<ActionResult> Invite(Guid workspaceId, InviteMemberRequest request, CancellationToken ct)
    {
        await CurrentWorkspace(workspaceId, false, ct);
        if (!Enum.TryParse<WorkspaceRole>(request.Role, true, out var role)) throw OrganisationsController.Invalid("membership.role.invalid", "The requested role is not supported.");
        var email = request.Email.Trim().ToLowerInvariant(); var now = DateTimeOffset.UtcNow;
        var user = await db.IdentityUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == email.ToUpperInvariant(), ct);
        if (user is null) { user = new IdentityUserRecord { Id = Guid.NewGuid(), Email = email, NormalizedEmail = email.ToUpperInvariant(), DisplayName = request.DisplayName.Trim(), Status = "Invited", Revision = 1, CreatedAt = now, UpdatedAt = now }; db.IdentityUsers.Add(user); }
        if (await db.WorkspaceMemberships.AnyAsync(item => item.WorkspaceId == workspaceId && item.UserId == user.Id && item.Status != "Removed", ct)) throw new ResourceConflictException("membership.exists", "The user already belongs to this workspace.");
        var token = ConvoLabAuthentication.NewSecret(); var membership = new WorkspaceMembershipRecord { Id = Guid.NewGuid(), WorkspaceId = workspaceId, UserId = user.Id, Role = role.ToString(), Status = "Invited", InvitationTokenHash = ConvoLabAuthentication.HashSecret(token), InvitationExpiresAt = now.AddDays(7), Revision = 1, CreatedAt = now, UpdatedAt = now };
        db.WorkspaceMemberships.Add(membership); AddAudit(workspaceId, "Membership.Invited", "WorkspaceMembership", membership.Id); await db.SaveChangesAsync(ct);
        return Created("", new { membership.Id, membership.UserId, membership.Role, membership.Status, activationToken = token, membership.InvitationExpiresAt });
    }

    [Authorize(Policy = WorkspacePermissions.ManageMembers)]
    [HttpPut("{workspaceId:guid}/members/{membershipId:guid}")]
    public async Task<ActionResult<WorkspaceMembershipRecord>> ChangeMembership(Guid workspaceId, Guid membershipId, ChangeMembershipRequest request, CancellationToken ct)
    {
        await CurrentWorkspace(workspaceId, false, ct);
        var item = await db.WorkspaceMemberships.SingleOrDefaultAsync(value => value.Id == membershipId && value.WorkspaceId == workspaceId, ct) ?? throw OrganisationsController.Missing("membership", membershipId);
        OrganisationsController.EnsureRevision(item.Revision, request.ExpectedRevision);
        if (!Enum.TryParse<WorkspaceRole>(request.Role, true, out var role) || !Enum.TryParse<MembershipStatus>(request.Status, true, out var status)) throw OrganisationsController.Invalid("membership.invalid", "Role or status is invalid.");
        var losesAdmin = item.Role == "Administrator" && (role != WorkspaceRole.Administrator || status != MembershipStatus.Active);
        if (losesAdmin && await db.WorkspaceMemberships.CountAsync(value => value.WorkspaceId == workspaceId && value.Role == "Administrator" && value.Status == "Active", ct) <= 1) throw new ResourceConflictException("membership.final_administrator", "Every workspace must retain an active Administrator.");
        item.Role = role.ToString(); item.Status = status.ToString(); item.Revision++; item.UpdatedAt = DateTimeOffset.UtcNow; AddAudit(workspaceId, "Membership.Changed", "WorkspaceMembership", item.Id); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [Authorize(Policy = WorkspacePermissions.ManageServiceAccounts)]
    [HttpGet("{workspaceId:guid}/service-accounts")]
    public async Task<ActionResult> ServiceAccounts(Guid workspaceId, CancellationToken ct)
    {
        await CurrentWorkspace(workspaceId, false, ct);
        return Ok(await db.ServiceAccounts.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).Select(item => new { item.Id, item.Name, item.ScopesJson, item.Status, item.ExpiresAt, item.LastUsedAt, item.Revision, item.CreatedAt }).ToListAsync(ct));
    }

    [Authorize(Policy = WorkspacePermissions.ManageServiceAccounts)]
    [HttpPost("{workspaceId:guid}/service-accounts")]
    public async Task<ActionResult> CreateServiceAccount(Guid workspaceId, CreateServiceAccountRequest request, CancellationToken ct)
    {
        await CurrentWorkspace(workspaceId, false, ct); var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid(); var secret = ConvoLabAuthentication.NewSecret();
        var item = new ServiceAccountRecord { Id = id, WorkspaceId = workspaceId, Name = request.Name.Trim(), SecretHash = ConvoLabAuthentication.HashSecret(secret), ScopesJson = JsonSerializer.Serialize(request.Scopes.Distinct()), Status = "Active", ExpiresAt = request.ExpiresAt, Revision = 1, CreatedAt = now, UpdatedAt = now };
        db.ServiceAccounts.Add(item); AddAudit(workspaceId, "ServiceAccount.Created", "ServiceAccount", item.Id); await db.SaveChangesAsync(ct);
        return Created("", new { item.Id, item.Name, credential = $"clsa_{id:N}_{secret}", item.ScopesJson, item.ExpiresAt, item.Revision });
    }

    [Authorize(Policy = WorkspacePermissions.ManageServiceAccounts)]
    [HttpPost("{workspaceId:guid}/service-accounts/{accountId:guid}/{lifecycleAction:regex(^(rotate|revoke)$)}")]
    public async Task<ActionResult> ServiceAccountLifecycle(Guid workspaceId, Guid accountId, string lifecycleAction, RevisionRequest request, CancellationToken ct)
    {
        await CurrentWorkspace(workspaceId, false, ct); var item = await db.ServiceAccounts.SingleOrDefaultAsync(value => value.Id == accountId && value.WorkspaceId == workspaceId, ct) ?? throw OrganisationsController.Missing("service_account", accountId);
        OrganisationsController.EnsureRevision(item.Revision, request.ExpectedRevision); string? credential = null;
        if (lifecycleAction == "revoke") item.Status = "Revoked"; else { var secret = ConvoLabAuthentication.NewSecret(); item.SecretHash = ConvoLabAuthentication.HashSecret(secret); credential = $"clsa_{item.Id:N}_{secret}"; }
        item.Revision++; item.UpdatedAt = DateTimeOffset.UtcNow; AddAudit(workspaceId, $"ServiceAccount.{lifecycleAction}", "ServiceAccount", item.Id); await db.SaveChangesAsync(ct); return Ok(new { item.Id, item.Status, item.Revision, credential });
    }

    [Authorize(Policy = WorkspacePermissions.ManageWorkspace)]
    [HttpGet("{workspaceId:guid}/audit")]
    public async Task<ActionResult> Audit(Guid workspaceId, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        await CurrentWorkspace(workspaceId, false, ct); take = Math.Clamp(take, 1, 500);
        return Ok(await db.WorkspaceAuditEvents.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).OrderByDescending(item => item.OccurredAt).Take(take).ToListAsync(ct));
    }

    private async Task<WorkspaceRecord> CurrentWorkspace(Guid id, bool tracking, CancellationToken ct)
    {
        var active = ClaimGuid("workspace_id"); if (active != id) throw OrganisationsController.Missing("workspace", id);
        var query = tracking ? db.Workspaces.AsQueryable() : db.Workspaces.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw OrganisationsController.Missing("workspace", id);
    }
    private Guid? ClaimGuid(string type) => Guid.TryParse(User.FindFirstValue(type), out var id) ? id : null;
    private void AddAudit(Guid workspaceId, string action, string type, Guid id) { var item = AuthController.Audit("Workspace", ClaimGuid("organisation_id"), workspaceId, User.FindFirstValue("actor_type") ?? "User", ClaimGuid(ClaimTypes.NameIdentifier), User.Identity?.Name ?? "Authenticated actor", action, type, id.ToString(), "Succeeded", HttpContext.TraceIdentifier); db.WorkspaceAuditEvents.Add(item); }
}

public sealed record CreateOrganisationRequest(string Name, string Slug);
public sealed record RevisionRequest(long ExpectedRevision);
public sealed record CreateWorkspaceRequest(string Name, string Slug, string? Description);
public sealed record UpdateWorkspaceRequest(string Name, string? Description, long ExpectedRevision);
public sealed record InviteMemberRequest(string Email, string DisplayName, string Role);
public sealed record ChangeMembershipRequest(string Role, string Status, long ExpectedRevision);
public sealed record CreateServiceAccountRequest(string Name, IReadOnlyList<string> Scopes, DateTimeOffset? ExpiresAt);
