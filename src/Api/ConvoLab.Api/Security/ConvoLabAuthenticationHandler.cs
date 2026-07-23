using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConvoLab.Api.Security;

public static class ConvoLabAuthentication
{
    public const string Scheme = "ConvoLab";
    public const string SessionCookie = "convolab_session";
    public const string AntiforgeryCookie = "XSRF-TOKEN";
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";

    public static string HashSecret(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string NewSecret(int bytes = 32) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class ConvoLabAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApplicationDbContext _db;
    private readonly WorkspaceRequestContext _workspace;
    private readonly IWebHostEnvironment _environment;

    public ConvoLabAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext db,
        WorkspaceRequestContext workspace,
        IWebHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _db = db; _workspace = workspace; _environment = environment;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_environment.IsEnvironment("Testing") && !Request.Headers.ContainsKey("Authorization") && !Request.Cookies.ContainsKey(ConvoLabAuthentication.SessionCookie))
            return BuildTestingPrincipal();

        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer clsa_", StringComparison.Ordinal))
            return await AuthenticateServiceAccountAsync(authorization[7..]);

        if (!Request.Cookies.TryGetValue(ConvoLabAuthentication.SessionCookie, out var token) || string.IsNullOrWhiteSpace(token))
            return AuthenticateResult.NoResult();

        var now = DateTimeOffset.UtcNow;
        var hash = ConvoLabAuthentication.HashSecret(token);
        var session = await _db.AuthenticationSessions.AsTracking().SingleOrDefaultAsync(item => item.TokenHash == hash);
        if (session is null || session.RevokedAt.HasValue || session.ExpiresAt <= now)
            return AuthenticateResult.Fail("The session is invalid or expired.");

        var user = await _db.IdentityUsers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == session.UserId && item.Status == "Active");
        if (user is null) return AuthenticateResult.Fail("The session user is unavailable.");

        WorkspaceMembershipRecord? membership = null;
        WorkspaceRecord? workspace = null;
        if (session.ActiveWorkspaceId.HasValue)
        {
            membership = await _db.WorkspaceMemberships.AsNoTracking().SingleOrDefaultAsync(item =>
                item.UserId == user.Id && item.WorkspaceId == session.ActiveWorkspaceId && item.Status == "Active");
            workspace = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(item => item.Id == session.ActiveWorkspaceId && item.Status == "Active");
            if (membership is null || workspace is null) return AuthenticateResult.Fail("The active workspace is unavailable.");
        }

        session.LastSeenAt = now;
        await _db.SaveChangesAsync(Context.RequestAborted);
        return BuildUserPrincipal(user, session, workspace, membership);
    }

    private async Task<AuthenticateResult> AuthenticateServiceAccountAsync(string credential)
    {
        var parts = credential.Split('_', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "clsa" || !Guid.TryParseExact(parts[1], "N", out var id))
            return AuthenticateResult.Fail("The service credential is invalid.");
        var now = DateTimeOffset.UtcNow;
        var account = await _db.ServiceAccounts.AsTracking().SingleOrDefaultAsync(item => item.Id == id);
        if (account is null || account.Status != "Active" || account.ExpiresAt <= now || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(account.SecretHash), Convert.FromHexString(ConvoLabAuthentication.HashSecret(parts[2]))))
            return AuthenticateResult.Fail("The service credential is invalid.");
        var workspace = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(item => item.Id == account.WorkspaceId && item.Status == "Active");
        if (workspace is null) return AuthenticateResult.Fail("The service workspace is unavailable.");
        account.LastUsedAt = now;
        await _db.SaveChangesAsync(Context.RequestAborted);
        _workspace.WorkspaceId = workspace.Id; _workspace.OrganisationId = workspace.OrganisationId;
        _workspace.ActorType = "ServiceAccount";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()), new(ClaimTypes.Name, account.Name),
            new("actor_type", "ServiceAccount"), new("workspace_id", workspace.Id.ToString()),
            new("organisation_id", workspace.OrganisationId.ToString())
        };
        foreach (var scope in JsonSerializer.Deserialize<string[]>(account.ScopesJson) ?? []) claims.Add(new("permission", scope));
        return Success(claims);
    }

    private AuthenticateResult BuildUserPrincipal(IdentityUserRecord user, AuthenticationSessionRecord session, WorkspaceRecord? workspace, WorkspaceMembershipRecord? membership)
    {
        _workspace.UserId = user.Id; _workspace.SessionId = session.Id; _workspace.ActorType = "User";
        _workspace.IsPlatformAdministrator = user.IsPlatformAdministrator;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email), new("actor_type", "User"), new("session_id", session.Id.ToString())
        };
        if (user.IsPlatformAdministrator) claims.Add(new("platform_administrator", "true"));
        if (workspace is not null && membership is not null && Enum.TryParse<WorkspaceRole>(membership.Role, out var role))
        {
            _workspace.WorkspaceId = workspace.Id; _workspace.OrganisationId = workspace.OrganisationId;
            _workspace.MembershipId = membership.Id; _workspace.Role = membership.Role;
            claims.Add(new("workspace_id", workspace.Id.ToString())); claims.Add(new("organisation_id", workspace.OrganisationId.ToString()));
            claims.Add(new("membership_id", membership.Id.ToString())); claims.Add(new(ClaimTypes.Role, membership.Role));
            foreach (var permission in WorkspacePermissions.For(role)) claims.Add(new("permission", permission));
        }
        return Success(claims);
    }

    private AuthenticateResult BuildTestingPrincipal()
    {
        _workspace.UserId = WorkspaceIdentityDefaults.BootstrapUserId; _workspace.OrganisationId = WorkspaceIdentityDefaults.OrganisationId;
        _workspace.WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId; _workspace.ActorType = "User"; _workspace.Role = "Administrator"; _workspace.IsPlatformAdministrator = true;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, WorkspaceIdentityDefaults.BootstrapUserId.ToString()), new(ClaimTypes.Name, "Test Administrator"),
            new("actor_type", "User"), new("workspace_id", WorkspaceIdentityDefaults.WorkspaceId.ToString()),
            new("organisation_id", WorkspaceIdentityDefaults.OrganisationId.ToString()), new(ClaimTypes.Role, "Administrator"), new("platform_administrator", "true")
        };
        claims.AddRange(WorkspacePermissions.For(WorkspaceRole.Administrator).Select(permission => new Claim("permission", permission)));
        return Success(claims);
    }

    private static AuthenticateResult Success(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, ConvoLabAuthentication.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ConvoLabAuthentication.Scheme));
    }
}
