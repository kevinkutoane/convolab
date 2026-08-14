using System.Diagnostics;
using System.Security.Claims;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using TicketReceivedContext = Microsoft.AspNetCore.Authentication.TicketReceivedContext;
using RemoteFailureContext = Microsoft.AspNetCore.Authentication.RemoteFailureContext;

namespace ConvoLab.Api.Security;

public static class EntraAuthentication
{
    public const string Scheme = "Entra";
    public const string ExternalCookieScheme = "ConvoLab.External";
    public const string InvitationCookie = "convolab_entra_invitation";

    public static bool IsSafeReturnUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string decoded;
        try { decoded = Uri.UnescapeDataString(value.Trim()); }
        catch (UriFormatException) { return false; }
        return decoded.StartsWith("/", StringComparison.Ordinal)
               && !decoded.StartsWith("//", StringComparison.Ordinal)
               && !decoded.StartsWith("/\\", StringComparison.Ordinal)
               && !decoded.Any(char.IsControl)
               && !Uri.TryCreate(decoded, UriKind.Absolute, out _);
    }

    public static string SafeReturnUrl(string? value) => IsSafeReturnUrl(value) ? value!.Trim() : "/";
}

public sealed class EntraDependencyEvidence
{
    private readonly object _gate = new();
    private OperationalDependencyState _state;
    private DateTimeOffset? _checkedAt;
    private string? _failureCode;
    private readonly TimeSpan _ttl;

    public EntraDependencyEvidence(IOptions<AuthenticationOptions> options)
    {
        _state = options.Value.Entra.Enabled && options.Value.Mode is ConvoLabAuthenticationMode.Entra or ConvoLabAuthenticationMode.Hybrid
            ? OperationalDependencyState.Configured
            : OperationalDependencyState.NotConfigured;
        _ttl = TimeSpan.FromSeconds(Math.Max(1, options.Value.Entra.DependencyEvidenceTtlSeconds));
    }

    public void Record(OperationalDependencyState state, string? failureCode = null)
    {
        lock (_gate) { _state = state; _checkedAt = DateTimeOffset.UtcNow; _failureCode = failureCode; }
    }

    public (OperationalDependencyState State, DateTimeOffset? CheckedAt, string? FailureCode) Snapshot()
    {
        lock (_gate)
        {
            if (_checkedAt is { } checkedAt && DateTimeOffset.UtcNow - checkedAt > _ttl)
                return (OperationalDependencyState.Configured, checkedAt, "authentication.entra.evidence_expired");
            return (_state, _checkedAt, _failureCode);
        }
    }
}

public sealed class ConvoLabOpenIdConnectEvents(
    ApplicationDbContext db,
    ISecretStore secretStore,
    SessionCookieService sessionCookies,
    IOptions<AuthenticationOptions> authenticationOptions,
    EntraDependencyEvidence dependencyEvidence,
    ILogger<ConvoLabOpenIdConnectEvents> logger) : OpenIdConnectEvents
{
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("authentication.entra.challenge");
        AddMetric(ConvoLabTelemetry.EntraChallenges, "started");
        return Task.CompletedTask;
    }

    public override async Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        var reference = authenticationOptions.Value.Entra.ClientSecretReference;
        var result = await secretStore.ResolveAsync(reference, context.HttpContext.RequestAborted);
        if (!result.IsResolved)
        {
            dependencyEvidence.Record(OperationalDependencyState.Degraded, "authentication.entra.client_secret_unavailable");
            context.Fail("authentication.entra.client_authentication_unavailable");
            return;
        }

        // The secret is supplied only to the token request and is never persisted in configuration or session state.
        context.TokenEndpointRequest!.ClientSecret = result.RevealValue();
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("authentication.entra.callback");
        var options = authenticationOptions.Value.Entra;
        var principal = context.Principal!;
        var issuer = principal.FindFirstValue("iss")?.Trim();
        var subject = principal.FindFirstValue("sub")?.Trim();
        var tenant = principal.FindFirstValue("tid")?.Trim();
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)
            || !string.Equals(tenant, options.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, "authentication.entra.claims_invalid");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var identity = await db.ExternalIdentities.SingleOrDefaultAsync(item =>
            item.Provider == "Entra" && item.Issuer == issuer && item.Subject == subject,
            context.HttpContext.RequestAborted);
        IdentityUserRecord? user;
        var linked = false;
        if (identity is not null)
        {
            if (!identity.IsActive)
            {
                await RejectAsync(context, "authentication.external_identity_disabled");
                return;
            }
            user = await db.IdentityUsers.SingleOrDefaultAsync(item => item.Id == identity.UserId,
                context.HttpContext.RequestAborted);
            if (user?.Status != "Active")
            {
                await RejectAsync(context, "authentication.user_inactive");
                return;
            }
        }
        else
        {
            identity = await LinkInvitationAsync(context, issuer, subject, tenant!, now);
            if (identity is null) return;
            user = await db.IdentityUsers.SingleAsync(item => item.Id == identity.UserId,
                context.HttpContext.RequestAborted);
            linked = true;
        }

        identity.EmailAtLastLogin = SafeEmail(principal);
        identity.DisplayNameAtLastLogin = principal.FindFirstValue("name")?.Trim() is { Length: > 0 } display
            ? display[..Math.Min(display.Length, 200)] : null;
        identity.LastLoginAt = now;
        identity.Revision++;

        var memberships = await db.WorkspaceMemberships.AsNoTracking()
            .Where(item => item.UserId == user!.Id && item.Status == "Active")
            .ToListAsync(context.HttpContext.RequestAborted);
        var membership = memberships.OrderBy(item => item.CreatedAt).FirstOrDefault();
        var sessionToken = ConvoLabAuthentication.NewSecret();
        var session = new AuthenticationSessionRecord
        {
            Id = Guid.NewGuid(), UserId = user.Id, ActiveWorkspaceId = membership?.WorkspaceId,
            TokenHash = ConvoLabAuthentication.HashSecret(sessionToken), CreatedAt = now, LastSeenAt = now,
            ExpiresAt = now.AddHours(8), AbsoluteExpiresAt = now.AddHours(24),
            AuthenticationProvider = "Entra", ExternalIdentityId = identity.Id, SessionFamilyId = Guid.NewGuid(),
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString()
        };
        db.AuthenticationSessions.Add(session);
        var organisationId = membership is null ? null : await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == membership.WorkspaceId).Select(item => (Guid?)item.OrganisationId)
            .SingleAsync(context.HttpContext.RequestAborted);
        var loginAudit = Controllers.AuthController.Audit(
            membership is null ? "Platform" : "Workspace", organisationId, membership?.WorkspaceId,
            "User", user.Id, "External identity user", "Authentication.EntraLogin",
            "ExternalIdentity", identity.Id.ToString(), "Succeeded", context.HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(loginAudit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(db, loginAudit, cancellationToken: context.HttpContext.RequestAborted);
        if (linked)
        {
            foreach (var action in new[]
                     {
                         "Authentication.ExternalIdentityLinked",
                         "Authentication.ExternalIdentityInvitationConsumed"
                     })
            {
                var linkingAudit = Controllers.AuthController.Audit(
                    membership is null ? "Platform" : "Workspace", organisationId, membership?.WorkspaceId,
                    "User", user.Id, "External identity user", action, "ExternalIdentity",
                    identity.Id.ToString(), "Succeeded", context.HttpContext.TraceIdentifier);
                db.WorkspaceAuditEvents.Add(linkingAudit);
                await AnalyticsOutboxFactory.EnqueueAuditAsync(db, linkingAudit,
                    cancellationToken: context.HttpContext.RequestAborted);
            }
        }
        await db.SaveChangesAsync(context.HttpContext.RequestAborted);

        context.Properties!.Items["convolab_session_token"] = sessionToken;
        context.Properties.Items["convolab_session_expires"] = session.ExpiresAt.ToString("O");
        AddMetric(ConvoLabTelemetry.EntraLoginSuccesses, "succeeded");
        if (linked) AddMetric(ConvoLabTelemetry.ExternalIdentityLinks, "invitation");
        var isMicrosoftAuthority = Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
                                   && authority.Host.Equals("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase);
        dependencyEvidence.Record(isMicrosoftAuthority
            ? OperationalDependencyState.LiveValidated
            : OperationalDependencyState.StubValidated);
    }

    public override Task TicketReceived(TicketReceivedContext context)
    {
        if (context.Properties is null
            || !context.Properties.Items.Remove("convolab_session_token", out var token)
            || !context.Properties.Items.Remove("convolab_session_expires", out var expiresValue)
            || !DateTimeOffset.TryParse(expiresValue, out var expires))
        {
            context.Fail("authentication.session_creation_failed");
            return Task.CompletedTask;
        }
        sessionCookies.Write(context.Response, token!, expires);
        var returnUrl = context.Properties.Items.TryGetValue("return_url", out var requested) ? requested : null;
        context.Response.Redirect(EntraAuthentication.SafeReturnUrl(returnUrl));
        context.HandleResponse();
        return Task.CompletedTask;
    }

    public override async Task RemoteFailure(RemoteFailureContext context)
    {
        logger.LogWarning("Entra authentication failed with safe code {FailureCode}", "authentication.entra.remote_failure");
        AddMetric(ConvoLabTelemetry.EntraLoginFailures, "remote_failure");
        dependencyEvidence.Record(OperationalDependencyState.Degraded, "authentication.entra.remote_failure");
        context.Response.Redirect("/login?error=authentication.external_login_failed");
        context.HandleResponse();
        await Task.CompletedTask;
    }

    private async Task<ExternalIdentityRecord?> LinkInvitationAsync(
        TokenValidatedContext context, string issuer, string subject, string tenant, DateTimeOffset now)
    {
        var options = authenticationOptions.Value.Entra;
        if (!options.AllowInvitationLinking
            || context.Properties is null
            || !context.Properties.Items.TryGetValue("invitation_hash", out var invitationHash)
            || string.IsNullOrWhiteSpace(invitationHash))
        {
            await RejectAsync(context, "authentication.external_identity_not_linked");
            return null;
        }
        var email = SafeEmail(context.Principal!);
        var verified = string.Equals(context.Principal!.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        if (email is null || !verified)
        {
            await RejectAsync(context, "authentication.invitation_verified_email_required");
            return null;
        }
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var invitation = await db.ExternalIdentityInvitations.SingleOrDefaultAsync(item =>
            item.TokenHash == invitationHash && item.Status == "Active" && item.RevokedAt == null,
            context.HttpContext.RequestAborted);
        if (invitation is null
            || invitation.ExpiresAt <= now
            || !string.Equals(invitation.ExpectedProvider, "Entra", StringComparison.Ordinal)
            || !string.Equals(invitation.ExpectedTenant, tenant, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(invitation.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
        {
            await RejectAsync(context, "authentication.external_identity_not_linked");
            return null;
        }
        var user = await db.IdentityUsers.SingleAsync(item => item.Id == invitation.UserId,
            context.HttpContext.RequestAborted);
        if (user.Status is not ("Active" or "Invited"))
        {
            await RejectAsync(context, "authentication.user_inactive");
            return null;
        }
        if (await db.ExternalIdentities.AnyAsync(item => item.UserId == user.Id && item.Provider == "Entra"
                && item.Issuer == issuer && item.IsActive, context.HttpContext.RequestAborted))
        {
            await RejectAsync(context, "authentication.external_identity_conflict");
            return null;
        }

        var identity = new ExternalIdentityRecord
        {
            Id = Guid.NewGuid(), UserId = user.Id, Provider = "Entra", Issuer = issuer, Subject = subject,
            TenantId = tenant, CreatedAt = now, LastLoginAt = now, IsActive = true, Revision = 1
        };
        db.ExternalIdentities.Add(identity);
        invitation.Status = "Consumed"; invitation.ConsumedAt = now;
        invitation.ConsumedByExternalIdentityId = identity.Id; invitation.Revision++;
        if (user.Status == "Invited")
        {
            user.Status = "Active"; user.UpdatedAt = now; user.Revision++;
            var memberships = await db.WorkspaceMemberships.Where(item => item.UserId == user.Id && item.Status == "Invited")
                .ToListAsync(context.HttpContext.RequestAborted);
            foreach (var membership in memberships)
            {
                membership.Status = "Active"; membership.InvitationTokenHash = null;
                membership.InvitationExpiresAt = null; membership.UpdatedAt = now; membership.Revision++;
            }
        }
        return identity;
    }

    private async Task RejectAsync(TokenValidatedContext context, string code)
    {
        AddMetric(ConvoLabTelemetry.EntraLoginFailures, code);
        if (code == "authentication.external_identity_not_linked")
            AddMetric(ConvoLabTelemetry.ExternalIdentityUnlinked, "rejected");
        var audit = Controllers.AuthController.Audit("Platform", null, null, "Anonymous", null,
            "External identity", "Authentication.EntraLogin", "ExternalIdentity", null, "Failed",
            context.HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(audit);
        await db.SaveChangesAsync(context.HttpContext.RequestAborted);
        context.Fail(code);
    }

    private static string? SafeEmail(ClaimsPrincipal principal) =>
        principal.FindFirstValue("email")?.Trim() is { Length: > 0 } email ? email : null;

    private static void AddMetric(System.Diagnostics.Metrics.Counter<long> counter, string outcome)
    {
        TagList tags = default; tags.Add("outcome", outcome); counter.Add(1, tags);
    }
}
