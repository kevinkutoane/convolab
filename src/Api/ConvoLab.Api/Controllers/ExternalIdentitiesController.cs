using System.Security.Claims;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Api.Security;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAdministrator")]
[Route("api/platform/users/{userId:guid}/external-identities")]
public sealed class ExternalIdentitiesController(
    ApplicationDbContext db,
    IOptionsSnapshot<AuthenticationOptions> authentication) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> List(Guid userId, CancellationToken ct)
    {
        await EnsureUserAsync(userId, ct);
        var records = await db.ExternalIdentities.AsNoTracking().Where(item => item.UserId == userId).ToListAsync(ct);
        var identities = records.OrderBy(item => item.Provider).ThenBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id, item.UserId, item.Provider,
                issuer = item.Provider == "Entra" ? "Microsoft Entra tenant authority" : "External authority",
                tenant = item.TenantId == authentication.Value.Entra.TenantId ? "Configured tenant" : "Other tenant",
                item.LastLoginAt, item.IsActive, item.DisabledAt, item.Revision
            }).ToList();
        return Ok(identities);
    }

    [HttpPost("invitations")]
    public async Task<ActionResult> Invite(Guid userId, CreateExternalIdentityInvitationRequest request, CancellationToken ct)
    {
        var user = await EnsureUserAsync(userId, ct);
        var options = authentication.Value.Entra;
        if (!options.Enabled || !options.AllowInvitationLinking)
            throw new RequestValidationException("authentication.invitation_linking_disabled", "External identity invitation linking is disabled.");
        if (request.ExpiresInHours is < 1 or > 168)
            throw new RequestValidationException("invitation.expiry_invalid", "Invitation expiry must be between one hour and seven days.");
        var now = DateTimeOffset.UtcNow;
        foreach (var existing in await db.ExternalIdentityInvitations
                     .Where(item => item.UserId == userId && item.Status == "Active").ToListAsync(ct))
        {
            existing.Status = "Revoked"; existing.RevokedAt = now; existing.Revision++;
        }
        var token = ConvoLabAuthentication.NewSecret();
        var invitation = new ExternalIdentityInvitationRecord
        {
            Id = Guid.NewGuid(), UserId = userId, InvitedEmail = user.Email,
            NormalizedEmail = user.NormalizedEmail, ExpectedTenant = options.TenantId,
            ExpectedProvider = "Entra", TokenHash = ConvoLabAuthentication.HashSecret(token),
            ExpiresAt = now.AddHours(request.ExpiresInHours ?? options.InvitationExpiryHours),
            CreatedBy = ActorId(), CreatedAt = now, Status = "Active", Revision = 1
        };
        db.ExternalIdentityInvitations.Add(invitation);
        await AuditAsync(userId, "Authentication.ExternalIdentityInvitationCreated", invitation.Id, "Succeeded", ct);
        await db.SaveChangesAsync(ct);
        return Created($"/api/platform/users/{userId}/external-identities", new
        {
            invitation.Id, invitation.ExpiresAt,
            invitationToken = token,
            note = "The single-use token is returned only in this response and is not stored in plaintext."
        });
    }

    [HttpPost("{identityId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid userId, Guid identityId, IdentityMutationRequest request, CancellationToken ct) =>
        await SetEnabledAsync(userId, identityId, request, false, "Authentication.ExternalIdentityDisabled", ct);

    [HttpPost("{identityId:guid}/enable")]
    public async Task<IActionResult> Enable(Guid userId, Guid identityId, IdentityMutationRequest request, CancellationToken ct) =>
        await SetEnabledAsync(userId, identityId, request, true, "Authentication.ExternalIdentityEnabled", ct);

    [HttpDelete("{identityId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, Guid identityId, [FromQuery] long expectedRevision,
        [FromQuery] bool confirmFinalSignInMethod, CancellationToken ct) =>
        await SetEnabledAsync(userId, identityId, new(expectedRevision, confirmFinalSignInMethod), false,
            "Authentication.ExternalIdentityRemoved", ct);

    private async Task<IActionResult> SetEnabledAsync(Guid userId, Guid identityId, IdentityMutationRequest request,
        bool enabled, string action, CancellationToken ct)
    {
        var targetUser = await EnsureUserAsync(userId, ct);
        var identity = await db.ExternalIdentities.SingleOrDefaultAsync(item => item.Id == identityId && item.UserId == userId, ct)
                       ?? throw new ResourceNotFoundException("external_identity.not_found", "The external identity was not found.");
        if (identity.Revision != request.ExpectedRevision)
            throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry.");
        if (!enabled)
        {
            var hasLocalCredential = await db.LocalCredentials.AnyAsync(item => item.UserId == userId, ct);
            var auth = authentication.Value;
            var hasLocal = hasLocalCredential && (auth.Mode == ConvoLabAuthenticationMode.Local
                || auth.Mode == ConvoLabAuthenticationMode.Hybrid && auth.Local.Enabled
                || auth.Local.BreakGlassEnabled && targetUser.IsPlatformAdministrator);
            var otherExternal = await db.ExternalIdentities.AnyAsync(item => item.UserId == userId && item.Id != identityId && item.IsActive, ct);
            if (!hasLocal && !otherExternal && !request.ConfirmFinalSignInMethod)
                throw new RequestValidationException("external_identity.final_sign_in_method_confirmation_required",
                    "Explicit confirmation is required before disabling the final sign-in method.");
            if (ActorId() == userId && !hasLocal && !otherExternal)
                throw new RequestValidationException("external_identity.self_lockout_prevented",
                    "You cannot remove your own final usable sign-in method.");
            identity.IsActive = false; identity.DisabledAt = DateTimeOffset.UtcNow; identity.DisabledBy = ActorId();
            var sessions = await db.AuthenticationSessions.Where(item => item.ExternalIdentityId == identity.Id && item.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var session in sessions)
            {
                session.RevokedAt = DateTimeOffset.UtcNow; session.RevocationReason = "ExternalIdentityDisabled";
                session.RevokedBy = ActorId();
            }
            if (sessions.Count > 0)
                await AuditAsync(userId, "Authentication.SessionRevokedForIdentityDisablement", identity.Id, "Succeeded", ct);
        }
        else
        {
            identity.IsActive = true; identity.DisabledAt = null; identity.DisabledBy = null;
        }
        identity.Revision++;
        await AuditAsync(userId, action, identity.Id, "Succeeded", ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IdentityUserRecord> EnsureUserAsync(Guid userId, CancellationToken ct) =>
        await db.IdentityUsers.SingleOrDefaultAsync(item => item.Id == userId, ct)
        ?? throw new ResourceNotFoundException("user.not_found", "The user was not found.");

    private async Task AuditAsync(Guid userId, string action, Guid resourceId, string outcome, CancellationToken ct)
    {
        var audit = AuthController.Audit("Platform", null, null, "User", ActorId(),
            User.Identity?.Name ?? "Platform administrator", action, "ExternalIdentity", resourceId.ToString(),
            outcome, HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(db, audit, cancellationToken: ct);
    }

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

public sealed record CreateExternalIdentityInvitationRequest(int? ExpiresInHours = null);
public sealed record IdentityMutationRequest(long ExpectedRevision, bool ConfirmFinalSignInMethod = false);
