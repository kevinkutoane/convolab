using System.Security.Claims;
using ConvoLab.Api.Security;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using ConvoLab.Application.Operations;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ApplicationDbContext db,
    IPasswordHasher<IdentityUserRecord> passwordHasher,
    SessionCookieService sessionCookies,
    IOptionsSnapshot<ConvoLab.Application.Operations.AuthenticationOptions> authentication,
    TimeProvider timeProvider) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("options")]
    public ActionResult AuthenticationOptions()
    {
        var configured = authentication.Value;
        var local = configured.Mode == ConvoLabAuthenticationMode.Local
                    || configured.Mode == ConvoLabAuthenticationMode.Hybrid && configured.Local.Enabled;
        return Ok(new
        {
            mode = configured.Mode.ToString(),
            localLoginAvailable = local,
            entraLoginAvailable = configured.Entra.Enabled && configured.Mode is ConvoLabAuthenticationMode.Entra or ConvoLabAuthenticationMode.Hybrid,
            breakGlassAvailable = configured.Local.BreakGlassEnabled && configured.Mode is ConvoLabAuthenticationMode.Entra or ConvoLabAuthenticationMode.Hybrid,
            entraLoginPath = "/api/auth/entra/login"
        });
    }

    [AllowAnonymous]
    [HttpPost("entra/prepare-invitation")]
    public async Task<ActionResult> PrepareEntraInvitation(PrepareEntraInvitationRequest request,
        [FromServices] IAntiforgery antiforgery)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new RequestValidationException("invitation.invalid", "The invitation is invalid or expired.");
        Response.Cookies.Append(EntraAuthentication.InvitationCookie,
            ConvoLabAuthentication.HashSecret(request.Token), new CookieOptions
            {
                HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10), Path = "/api/auth/entra"
            });
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("entra/login")]
    public IActionResult EntraLogin([FromQuery] string? returnUrl = null)
    {
        var configured = authentication.Value;
        if (!configured.Entra.Enabled || configured.Mode == ConvoLabAuthenticationMode.Local)
            return NotFound();
        var properties = new AuthenticationProperties { RedirectUri = EntraAuthentication.SafeReturnUrl(returnUrl) };
        properties.Items["return_url"] = EntraAuthentication.SafeReturnUrl(returnUrl);
        if (Request.Cookies.TryGetValue(EntraAuthentication.InvitationCookie, out var invitationHash)
            && !string.IsNullOrWhiteSpace(invitationHash))
            properties.Items["invitation_hash"] = invitationHash;
        Response.Cookies.Delete(EntraAuthentication.InvitationCookie, new CookieOptions { Path = "/api/auth/entra" });
        return Challenge(properties, EntraAuthentication.Scheme);
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var configured = authentication.Value;
        if (configured.Mode == ConvoLabAuthenticationMode.Entra
            || configured.Mode == ConvoLabAuthenticationMode.Hybrid && !configured.Local.Enabled)
            return NotFound();
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("authentication.login");
        var email = request.Email?.Trim().ToUpperInvariant() ?? string.Empty;
        var user = await db.IdentityUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == email, ct);
        var credential = user is null ? null : await db.LocalCredentials.SingleOrDefaultAsync(item => item.UserId == user.Id, ct);
        var now = timeProvider.GetUtcNow();
        var valid = user is not null && credential is not null && user.Status == "Active" && (!credential.LockedUntil.HasValue || credential.LockedUntil <= now)
            && passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, request.Password ?? string.Empty) != PasswordVerificationResult.Failed;
        if (!valid)
        {
            ConvoLabTelemetry.AuthenticationFailures.Add(1);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "invalid_credentials");
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
            ExpiresAt = now.AddHours(8), AbsoluteExpiresAt = now.AddHours(24), AuthenticationProvider = "Local",
            SessionFamilyId = Guid.NewGuid(), IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
        var organisationId = membership is null ? null : await db.Workspaces.AsNoTracking().Where(item => item.Id == membership.WorkspaceId).Select(item => (Guid?)item.OrganisationId).SingleAsync(ct);
        db.AuthenticationSessions.Add(session);
        var loginAudit = Audit(membership is null ? "Platform" : "Workspace", organisationId, membership?.WorkspaceId, "User", user.Id, user.Email, "Authentication.Login", "AuthenticationSession", session.Id.ToString(), "Succeeded", HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(loginAudit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(db, loginAudit, cancellationToken: ct);
        await db.SaveChangesAsync(ct);
        ConvoLabTelemetry.AuthenticationLogins.Add(1);
        WriteSessionCookie(token, session.ExpiresAt);
        return Ok(await DescribeAsync(user, session, ct));
    }

    [AllowAnonymous]
    [EnableRateLimiting("break-glass-login")]
    [HttpPost("break-glass/login")]
    public async Task<ActionResult<AuthSessionResponse>> BreakGlassLogin(LoginRequest request, CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("authentication.break_glass");
        var configured = authentication.Value;
        if (!configured.Local.BreakGlassEnabled
            || configured.Mode is not (ConvoLabAuthenticationMode.Entra or ConvoLabAuthenticationMode.Hybrid))
            return NotFound();
        var limits = configured.Local.BreakGlass;
        var normalized = request.Email?.Trim().ToUpperInvariant() ?? string.Empty;
        const int maximumConcurrencyAttempts = 10;
        for (var concurrencyAttempt = 0; concurrencyAttempt < maximumConcurrencyAttempts; concurrencyAttempt++)
        {
        var user = await db.IdentityUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == normalized, ct);
        var credential = user is null ? null : await db.LocalCredentials.SingleOrDefaultAsync(item => item.UserId == user.Id, ct);
        var now = timeProvider.GetUtcNow();
        var eligible = user is { Status: "Active", IsPlatformAdministrator: true } && credential is not null;
        var locked = eligible && credential!.BreakGlassLockedUntil is { } lockedUntil && lockedUntil > now;
        if (eligible && credential!.BreakGlassLockedUntil is { } expired && expired <= now)
        {
            credential.BreakGlassFailedAttempts = 0;
            credential.BreakGlassLockedUntil = null;
        }
        var validPassword = eligible && !locked
            && passwordHasher.VerifyHashedPassword(user!, credential!.PasswordHash, request.Password ?? string.Empty)
               != PasswordVerificationResult.Failed;
        if (!validPassword)
        {
            var thresholdReached = false;
            if (eligible && !locked)
            {
                credential!.BreakGlassFailedAttempts++;
                credential.BreakGlassLastFailedAt = now;
                thresholdReached = credential.BreakGlassFailedAttempts >= limits.MaximumAttempts;
                if (thresholdReached) credential.BreakGlassLockedUntil = now.AddMinutes(limits.LockoutMinutes);
                credential.BreakGlassRevision++;
                credential.UpdatedAt = now;
            }
            AddBreakGlassFailureEvidence(locked, thresholdReached);
            try
            {
                await db.SaveChangesAsync(ct);
                RecordBreakGlassMetric("denied", "invalid_credentials", locked || thresholdReached ? "locked" : "unlocked");
                return UnauthorizedProblem("authentication.break_glass_denied", "Emergency administrator access was denied.");
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                if (concurrencyAttempt < maximumConcurrencyAttempts - 1) continue;
                break;
            }
        }
        credential!.BreakGlassFailedAttempts = 0;
        credential.BreakGlassLockedUntil = null;
        credential.BreakGlassRevision++;
        credential.UpdatedAt = now;
        var administrator = user!;
        var memberships = await db.WorkspaceMemberships.AsNoTracking()
            .Where(item => item.UserId == administrator.Id && item.Status == "Active")
            .ToListAsync(ct);
        var membership = memberships.OrderBy(item => item.CreatedAt).FirstOrDefault();
        var token = ConvoLabAuthentication.NewSecret();
        var session = new AuthenticationSessionRecord
        {
            Id = Guid.NewGuid(), UserId = administrator.Id, ActiveWorkspaceId = membership?.WorkspaceId,
            TokenHash = ConvoLabAuthentication.HashSecret(token), CreatedAt = now, LastSeenAt = now,
            ExpiresAt = now.AddHours(1), AbsoluteExpiresAt = now.AddHours(1),
            AuthenticationProvider = "BreakGlass", SessionFamilyId = Guid.NewGuid(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = Request.Headers.UserAgent.ToString()
        };
        db.AuthenticationSessions.Add(session);
        var audit = Audit("Platform", null, membership?.WorkspaceId, "User", administrator.Id, "Emergency administrator",
            "Authentication.BreakGlassLogin", "AuthenticationSession", session.Id.ToString(), "Succeeded",
            HttpContext.TraceIdentifier);
        audit.DetailJson = "{\"severity\":\"High\"}";
        db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(db, audit, cancellationToken: ct);
        await db.SaveChangesAsync(ct);
        RecordBreakGlassMetric("succeeded", "none", "unlocked");
        WriteSessionCookie(token, session.ExpiresAt);
        return Ok(await DescribeAsync(administrator, session, ct));
        }
        RecordBreakGlassMetric("denied", "concurrency_conflict", "unknown");
        return UnauthorizedProblem("authentication.break_glass_denied", "Emergency administrator access was denied.");
    }

    private void AddBreakGlassFailureEvidence(bool alreadyLocked, bool thresholdReached)
    {
        var failure = Audit("Platform", null, null, "Anonymous", null, "Emergency administrator",
            "Authentication.BreakGlassFailure", "IdentityUser", null, "Denied", HttpContext.TraceIdentifier);
        failure.DetailJson = $"{{\"severity\":\"High\",\"lockoutState\":\"{(alreadyLocked || thresholdReached ? "Locked" : "Unlocked")}\"}}";
        db.WorkspaceAuditEvents.Add(failure);
        if (!thresholdReached) return;
        var lockout = Audit("Platform", null, null, "Anonymous", null, "Emergency administrator",
            "Authentication.BreakGlassLocked", "IdentityUser", null, "Denied", HttpContext.TraceIdentifier);
        lockout.DetailJson = "{\"severity\":\"High\",\"lockoutState\":\"Locked\"}";
        db.WorkspaceAuditEvents.Add(lockout);
    }

    private static void RecordBreakGlassMetric(string outcome, string failureCode, string lockoutState)
    {
        System.Diagnostics.TagList tags = default;
        tags.Add("outcome", outcome); tags.Add("failure_code", failureCode); tags.Add("lockout_state", lockoutState);
        ConvoLabTelemetry.BreakGlassLogins.Add(1, tags);
    }

    [AllowAnonymous]
    [HttpGet("antiforgery")]
    public ActionResult Antiforgery([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store";
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
        var now = timeProvider.GetUtcNow(); var token = ConvoLabAuthentication.NewSecret(); var hash = ConvoLabAuthentication.HashSecret(token);
        current.RevokedAt = now; current.ReplacedByTokenHash = hash;
        var replacement = new AuthenticationSessionRecord { Id = Guid.NewGuid(), UserId = current.UserId, ActiveWorkspaceId = current.ActiveWorkspaceId, TokenHash = hash, CreatedAt = now, LastSeenAt = now, ExpiresAt = new[] { now.AddHours(8), current.AbsoluteExpiresAt }.Min(), AbsoluteExpiresAt = current.AbsoluteExpiresAt, AuthenticationProvider = current.AuthenticationProvider, ExternalIdentityId = current.ExternalIdentityId, SessionFamilyId = current.SessionFamilyId == Guid.Empty ? current.Id : current.SessionFamilyId, IpAddress = current.IpAddress, UserAgent = current.UserAgent };
        db.AuthenticationSessions.Add(replacement); await db.SaveChangesAsync(ct); WriteSessionCookie(token, replacement.ExpiresAt);
        var user = await db.IdentityUsers.AsNoTracking().SingleAsync(item => item.Id == current.UserId, ct);
        return Ok(await DescribeAsync(user, replacement, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromQuery] bool external, CancellationToken ct)
    {
        var sessionId = ClaimGuid("session_id");
        var provider = "Local";
        if (sessionId.HasValue)
        {
            var session = await db.AuthenticationSessions.SingleOrDefaultAsync(item => item.Id == sessionId, ct);
            provider = session?.AuthenticationProvider ?? "Local";
            if (session is not null && !session.RevokedAt.HasValue)
            {
                session.RevokedAt = timeProvider.GetUtcNow();
                session.RevocationReason = "UserLogout";
                session.RevokedBy = ClaimGuid(ClaimTypes.NameIdentifier);
            }
            var workspaceId = session?.ActiveWorkspaceId;
            var organisationId = workspaceId.HasValue
                ? await db.Workspaces.AsNoTracking()
                    .Where(item => item.Id == workspaceId)
                    .Select(item => (Guid?)item.OrganisationId)
                    .SingleOrDefaultAsync(ct)
                : null;
            var logoutAudit = Audit(
                workspaceId.HasValue ? "Workspace" : "Platform",
                organisationId,
                workspaceId,
                User.FindFirstValue("actor_type") ?? "User",
                ClaimGuid(ClaimTypes.NameIdentifier),
                User.Identity?.Name ?? "Authenticated actor",
                "Authentication.Logout",
                "AuthenticationSession",
                sessionId.Value.ToString(),
                "Succeeded",
                HttpContext.TraceIdentifier);
            db.WorkspaceAuditEvents.Add(logoutAudit);
            await AnalyticsOutboxFactory.EnqueueAuditAsync(
                db,
                logoutAudit,
                cancellationToken: ct);
            await db.SaveChangesAsync(ct);
        }
        sessionCookies.Delete(Response);
        ConvoLabTelemetry.AuthenticationLogouts.Add(1);
        if (external && provider == "Entra")
            return SignOut(new AuthenticationProperties
            {
                RedirectUri = EntraAuthentication.SafeReturnUrl(authentication.Value.Entra.PostLogoutRedirectUri)
            }, EntraAuthentication.Scheme);
        return NoContent();
    }

    [HttpGet("sessions")]
    public async Task<ActionResult> Sessions(CancellationToken ct)
    {
        var userId = ClaimGuid(ClaimTypes.NameIdentifier) ?? throw new ResourceNotFoundException("auth.user_not_found", "The user was not found.");
        var currentSessionId = ClaimGuid("session_id");
        var now = timeProvider.GetUtcNow();
        var sessions = await db.AuthenticationSessions.AsNoTracking()
            .Where(item => item.UserId == userId && item.RevokedAt == null).ToListAsync(ct);
        return Ok(sessions.Where(item => item.ExpiresAt > now).Select(item => new { item.Id, item.CreatedAt, item.LastSeenAt, item.ExpiresAt, item.AuthenticationProvider, item.IpAddress, item.UserAgent, current = item.Id == currentSessionId }));
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        var userId = ClaimGuid(ClaimTypes.NameIdentifier) ?? throw new ResourceNotFoundException("auth.user_not_found", "The user was not found.");
        var session = await db.AuthenticationSessions.SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("auth.session_not_found", "The session was not found.");
        if (!session.RevokedAt.HasValue)
        {
            session.RevokedAt = timeProvider.GetUtcNow(); session.RevocationReason = "UserRevoked"; session.RevokedBy = userId;
        }
        await db.SaveChangesAsync(ct);
        if (ClaimGuid("session_id") == sessionId) sessionCookies.Delete(Response);
        return NoContent();
    }

    [HttpPost("workspace")]
    public async Task<ActionResult<AuthSessionResponse>> SwitchWorkspace(SwitchWorkspaceRequest request, CancellationToken ct)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("workspace.selection");
        var sessionId = ClaimGuid("session_id") ?? throw new ResourceNotFoundException("auth.session_not_found", "The session was not found.");
        var userId = ClaimGuid(ClaimTypes.NameIdentifier)!.Value;
        var membership = await db.WorkspaceMemberships.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.WorkspaceId == request.WorkspaceId && item.Status == "Active", ct)
            ?? throw new ResourceNotFoundException("workspace.not_found", $"Workspace '{request.WorkspaceId}' was not found.");
        var session = await db.AuthenticationSessions.SingleAsync(item => item.Id == sessionId, ct); session.ActiveWorkspaceId = membership.WorkspaceId;
        var organisationId = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == membership.WorkspaceId)
            .Select(item => item.OrganisationId)
            .SingleAsync(ct);
        var audit = Audit(
            "Workspace",
            organisationId,
            membership.WorkspaceId,
            User.FindFirstValue("actor_type") ?? "User",
            userId,
            User.Identity?.Name ?? "Authenticated actor",
            "Workspace.Selected",
            "Workspace",
            membership.WorkspaceId.ToString(),
            "Succeeded",
            HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(db, audit, cancellationToken: ct);
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
        var hash = ConvoLabAuthentication.HashSecret(request.Token ?? string.Empty); var now = timeProvider.GetUtcNow();
        var membership = await db.WorkspaceMemberships.SingleOrDefaultAsync(
            item => item.InvitationTokenHash == hash && item.Status == "Invited", ct);
        if (membership?.InvitationExpiresAt is not { } expiresAt || expiresAt <= now)
            throw new RequestValidationException("invitation.invalid", "The invitation is invalid or expired.");
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
        return new AuthSessionResponse(user.Id, user.Email, user.DisplayName, user.IsPlatformAdministrator, session.AuthenticationProvider, session.ExpiresAt, session.ActiveWorkspaceId, choices);
    }

    private void WriteSessionCookie(string token, DateTimeOffset expires) =>
        sessionCookies.Write(Response, token, expires);
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
public sealed record PrepareEntraInvitationRequest(string? Token);
public sealed record WorkspaceChoice(Guid Id, Guid OrganisationId, string Name, string Role);
public sealed record AuthSessionResponse(Guid UserId, string Email, string DisplayName, bool IsPlatformAdministrator, string AuthenticationProvider, DateTimeOffset ExpiresAt, Guid? ActiveWorkspaceId, IReadOnlyList<WorkspaceChoice> Workspaces);
