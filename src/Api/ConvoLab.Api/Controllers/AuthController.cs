using System.Security.Claims;
using ConvoLab.Api.Security;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ApplicationDbContext db, IPasswordHasher<IdentityUserRecord> passwordHasher) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToUpperInvariant() ?? string.Empty;
        var user = await db.IdentityUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == email, ct);
        var credential = user is null ? null : await db.LocalCredentials.SingleOrDefaultAsync(item => item.UserId == user.Id, ct);
        var now = DateTimeOffset.UtcNow;
        var valid = user is not null && credential is not null && user.Status == "Active" && (!credential.LockedUntil.HasValue || credential.LockedUntil <= now)
            && passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, request.Password ?? string.Empty) != PasswordVerificationResult.Failed;
        if (!valid)
        {
            if (credential is not null)
            {
                credential.FailedAttempts++;
                if (credential.FailedAttempts >= 5) { credential.LockedUntil = now.AddMinutes(15); credential.FailedAttempts = 0; }
            }
            db.WorkspaceAuditEvents.Add(Audit("Platform", null, null, "Anonymous", null, request.Email ?? "", "Authentication.Login", "IdentityUser", user?.Id.ToString(), "Failed", HttpContext.TraceIdentifier));
            await db.SaveChangesAsync(ct);
            return UnauthorizedProblem("auth.invalid_credentials", "The email address or password is incorrect.");
        }

        credential!.FailedAttempts = 0; credential.LockedUntil = null; credential.UpdatedAt = now;
        var membership = (await db.WorkspaceMemberships.AsNoTracking().Where(item => item.UserId == user!.Id && item.Status == "Active").ToListAsync(ct)).OrderBy(item => item.CreatedAt).FirstOrDefault();
        var token = ConvoLabAuthentication.NewSecret();
        var session = new AuthenticationSessionRecord
        {
            Id = Guid.NewGuid(), UserId = user!.Id, ActiveWorkspaceId = membership?.WorkspaceId,
            TokenHash = ConvoLabAuthentication.HashSecret(token), CreatedAt = now, LastSeenAt = now,
            ExpiresAt = now.AddHours(8), IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
        var organisationId = membership is null ? null : await db.Workspaces.AsNoTracking().Where(item => item.Id == membership.WorkspaceId).Select(item => (Guid?)item.OrganisationId).SingleAsync(ct);
        db.AuthenticationSessions.Add(session);
        db.WorkspaceAuditEvents.Add(Audit(membership is null ? "Platform" : "Workspace", organisationId, membership?.WorkspaceId, "User", user.Id, user.Email, "Authentication.Login", "AuthenticationSession", session.Id.ToString(), "Succeeded", HttpContext.TraceIdentifier));
        await db.SaveChangesAsync(ct);
        WriteSessionCookie(token, session.ExpiresAt);
        return Ok(await DescribeAsync(user, session, ct));
    }

    [AllowAnonymous]
    [HttpGet("antiforgery")]
    public ActionResult Antiforgery([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken, headerName = ConvoLabAuthentication.AntiforgeryHeader });
    }

    [HttpGet("session")]
    [HttpGet("me")]
    public async Task<ActionResult<AuthSessionResponse>> Session(CancellationToken ct)
    {
        var sessionId = ClaimGuid("session_id");
        var userId = ClaimGuid(ClaimTypes.NameIdentifier);
        if (!sessionId.HasValue || !userId.HasValue) return UnauthorizedProblem("auth.session_required", "An interactive session is required.");
        var session = await db.AuthenticationSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId, ct);
        var user = await db.IdentityUsers.AsNoTracking().SingleAsync(item => item.Id == userId, ct);
        return Ok(await DescribeAsync(user, session, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(CancellationToken ct)
    {
        var sessionId = ClaimGuid("session_id") ?? throw new ResourceNotFoundException("auth.session_not_found", "The session was not found.");
        var current = await db.AuthenticationSessions.SingleOrDefaultAsync(item => item.Id == sessionId && item.RevokedAt == null, ct)
            ?? throw new ResourceNotFoundException("auth.session_not_found", "The session was not found.");
        var now = DateTimeOffset.UtcNow; var token = ConvoLabAuthentication.NewSecret(); var hash = ConvoLabAuthentication.HashSecret(token);
        current.RevokedAt = now; current.ReplacedByTokenHash = hash;
        var replacement = new AuthenticationSessionRecord { Id = Guid.NewGuid(), UserId = current.UserId, ActiveWorkspaceId = current.ActiveWorkspaceId, TokenHash = hash, CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddHours(8), IpAddress = current.IpAddress, UserAgent = current.UserAgent };
        db.AuthenticationSessions.Add(replacement); await db.SaveChangesAsync(ct); WriteSessionCookie(token, replacement.ExpiresAt);
        var user = await db.IdentityUsers.AsNoTracking().SingleAsync(item => item.Id == current.UserId, ct);
        return Ok(await DescribeAsync(user, replacement, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sessionId = ClaimGuid("session_id");
        if (sessionId.HasValue)
        {
            var session = await db.AuthenticationSessions.SingleOrDefaultAsync(item => item.Id == sessionId, ct);
            if (session is not null && !session.RevokedAt.HasValue) session.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        Response.Cookies.Delete(ConvoLabAuthentication.SessionCookie);
        return NoContent();
    }

    [HttpGet("sessions")]
    public async Task<ActionResult> Sessions(CancellationToken ct)
    {
        var userId = ClaimGuid(ClaimTypes.NameIdentifier) ?? throw new ResourceNotFoundException("auth.user_not_found", "The user was not found.");
        var currentSessionId = ClaimGuid("session_id");
        return Ok(await db.AuthenticationSessions.AsNoTracking().Where(item => item.UserId == userId && item.RevokedAt == null && item.ExpiresAt > DateTimeOffset.UtcNow).Select(item => new { item.Id, item.CreatedAt, item.LastSeenAt, item.ExpiresAt, item.IpAddress, item.UserAgent, current = item.Id == currentSessionId }).ToListAsync(ct));
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        var userId = ClaimGuid(ClaimTypes.NameIdentifier) ?? throw new ResourceNotFoundException("auth.user_not_found", "The user was not found.");
        var session = await db.AuthenticationSessions.SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("auth.session_not_found", "The session was not found.");
        session.RevokedAt ??= DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
        if (ClaimGuid("session_id") == sessionId) Response.Cookies.Delete(ConvoLabAuthentication.SessionCookie);
        return NoContent();
    }

    [HttpPost("workspace")]
    public async Task<ActionResult<AuthSessionResponse>> SwitchWorkspace(SwitchWorkspaceRequest request, CancellationToken ct)
    {
        var sessionId = ClaimGuid("session_id") ?? throw new ResourceNotFoundException("auth.session_not_found", "The session was not found.");
        var userId = ClaimGuid(ClaimTypes.NameIdentifier)!.Value;
        var membership = await db.WorkspaceMemberships.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.WorkspaceId == request.WorkspaceId && item.Status == "Active", ct)
            ?? throw new ResourceNotFoundException("workspace.not_found", $"Workspace '{request.WorkspaceId}' was not found.");
        var session = await db.AuthenticationSessions.SingleAsync(item => item.Id == sessionId, ct); session.ActiveWorkspaceId = membership.WorkspaceId;
        await db.SaveChangesAsync(ct);
        var user = await db.IdentityUsers.AsNoTracking().SingleAsync(item => item.Id == userId, ct);
        return Ok(await DescribeAsync(user, session, ct));
    }

    [AllowAnonymous]
    [HttpPost("invitations/accept")]
    public async Task<IActionResult> AcceptInvitation(AcceptInvitationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            throw new RequestValidationException("credential.password.weak", "Passwords must contain at least 12 characters.");
        var hash = ConvoLabAuthentication.HashSecret(request.Token ?? string.Empty); var now = DateTimeOffset.UtcNow;
        var membership = await db.WorkspaceMemberships.SingleOrDefaultAsync(item => item.InvitationTokenHash == hash && item.Status == "Invited" && item.InvitationExpiresAt > now, ct)
            ?? throw new RequestValidationException("invitation.invalid", "The invitation is invalid or expired.");
        var user = await db.IdentityUsers.SingleAsync(item => item.Id == membership.UserId, ct);
        db.LocalCredentials.Add(new LocalCredentialRecord { UserId = user.Id, PasswordHash = passwordHasher.HashPassword(user, request.Password ?? string.Empty), UpdatedAt = now });
        user.Status = "Active"; user.UpdatedAt = now; user.Revision++; membership.Status = "Active"; membership.InvitationTokenHash = null; membership.InvitationExpiresAt = null; membership.Revision++; membership.UpdatedAt = now;
        await db.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<AuthSessionResponse> DescribeAsync(IdentityUserRecord user, AuthenticationSessionRecord session, CancellationToken ct)
    {
        var memberships = await db.WorkspaceMemberships.AsNoTracking().Where(item => item.UserId == user.Id && item.Status == "Active").ToListAsync(ct);
        var ids = memberships.Select(item => item.WorkspaceId).ToArray();
        var workspaces = await db.Workspaces.AsNoTracking().Where(item => ids.Contains(item.Id) && item.Status == "Active").ToListAsync(ct);
        var choices = workspaces.Select(workspace => { var membership = memberships.Single(item => item.WorkspaceId == workspace.Id); return new WorkspaceChoice(workspace.Id, workspace.OrganisationId, workspace.Name, membership.Role); }).ToArray();
        return new AuthSessionResponse(user.Id, user.Email, user.DisplayName, user.IsPlatformAdministrator, session.ExpiresAt, session.ActiveWorkspaceId, choices);
    }

    private void WriteSessionCookie(string token, DateTimeOffset expires) => Response.Cookies.Append(ConvoLabAuthentication.SessionCookie, token, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, Expires = expires, Path = "/" });
    private Guid? ClaimGuid(string type) => Guid.TryParse(User.FindFirstValue(type), out var id) ? id : null;
    private ObjectResult UnauthorizedProblem(string code, string detail)
    {
        var problem = new ProblemDetails { Status = 401, Title = "Authentication failed", Detail = detail, Type = $"https://errors.convolab.dev/{code}" };
        problem.Extensions["code"] = code; problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = 401, ContentTypes = { "application/problem+json" } };
    }
    internal static AuditEventRecord Audit(string scope, Guid? organisationId, Guid? workspaceId, string actorType, Guid? actorId, string actorDisplay, string action, string resourceType, string? resourceId, string outcome, string correlationId) => new() { Id = Guid.NewGuid(), Scope = scope, OrganisationId = organisationId, WorkspaceId = workspaceId, ActorType = actorType, ActorId = actorId, ActorDisplay = actorDisplay, Action = action, ResourceType = resourceType, ResourceId = resourceId, Outcome = outcome, CorrelationId = correlationId, OccurredAt = DateTimeOffset.UtcNow };
}

public sealed record LoginRequest(string? Email, string? Password);
public sealed record SwitchWorkspaceRequest(Guid WorkspaceId);
public sealed record AcceptInvitationRequest(string? Token, string? Password);
public sealed record WorkspaceChoice(Guid Id, Guid OrganisationId, string Name, string Role);
public sealed record AuthSessionResponse(Guid UserId, string Email, string DisplayName, bool IsPlatformAdministrator, DateTimeOffset ExpiresAt, Guid? ActiveWorkspaceId, IReadOnlyList<WorkspaceChoice> Workspaces);
