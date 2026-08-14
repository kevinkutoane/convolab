using System.IdentityModel.Tokens.Jwt;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConvoLab.Api.Security;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ConvoLab.Api.IntegrationTests;

public sealed class MockEntraOidcTests
{
    [Fact]
    public async Task Known_linked_identity_creates_an_opaque_convolab_session_and_stub_evidence()
    {
        await using var factory = new MockEntraFactory();
        await factory.SeedLinkedUserAsync(activeIdentity: true, activeUser: true);
        factory.CommitFailure.ObserveAuthenticationCommit = true;
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);
        var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var payload = await session.Content.ReadAsStringAsync();
        Assert.Contains("\"authenticationProvider\":\"Entra\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Subject, payload, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdSession = await db.AuthenticationSessions.AsNoTracking().SingleAsync(
            item => item.ExternalIdentityId == MockEntraFactory.IdentityId);
        Assert.Equal(MockEntraFactory.UserId, createdSession.UserId);
        Assert.Equal(WorkspaceIdentityDefaults.WorkspaceId, createdSession.ActiveWorkspaceId);
        Assert.NotEqual(createdSession.TokenHash, createdSession.Id.ToString());
        Assert.True(factory.CommitFailure.AuthenticationCommitCompleted);
        Assert.Equal(ConvoLab.Application.Operations.OperationalDependencyState.StubValidated,
            factory.Services.GetRequiredService<EntraDependencyEvidence>().Snapshot().State);
    }

    [Fact]
    public async Task Valid_single_use_invitation_links_once_without_email_identity_matching()
    {
        await using var factory = new MockEntraFactory();
        var token = await factory.SeedInvitationAsync();
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, token);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.ExternalIdentities.CountAsync());
        Assert.Equal("Consumed", await db.ExternalIdentityInvitations.Select(item => item.Status).SingleAsync());
        Assert.Equal(factory.Subject, await db.ExternalIdentities.Select(item => item.Subject).SingleAsync());
    }

    [Fact]
    public async Task Valid_invitation_without_email_or_email_verified_links_successfully()
    {
        await using var factory = new MockEntraFactory();
        factory.Backchannel.IncludeEmail = false;
        var token = await factory.SeedInvitationAsync();
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, token);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.ExternalIdentities.CountAsync());
        Assert.Equal("Consumed", await db.ExternalIdentityInvitations.Select(item => item.Status).SingleAsync());
    }

    [Theory]
    [InlineData("preferred_username")]
    [InlineData("upn")]
    public async Task Profile_login_names_are_not_authoritative_and_conflicting_email_is_rejected(string claimType)
    {
        await using var factory = new MockEntraFactory();
        var token = await factory.SeedInvitationAsync();
        factory.Backchannel.EmailOverride = "conflict@example.test";
        if (claimType == "preferred_username") factory.Backchannel.PreferredUsername = factory.InvitedEmail;
        else factory.Backchannel.Upn = factory.InvitedEmail;
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, token);
        Assert.StartsWith("/login?error=", callback.Headers.Location?.OriginalString, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.ExternalIdentities.CountAsync());
        Assert.Equal("Active", await db.ExternalIdentityInvitations.Select(item => item.Status).SingleAsync());
    }

    [Theory]
    [InlineData("preferred_username")]
    [InlineData("upn")]
    public async Task Profile_login_names_without_email_do_not_block_valid_invitation(string claimType)
    {
        await using var factory = new MockEntraFactory();
        var token = await factory.SeedInvitationAsync();
        factory.Backchannel.IncludeEmail = false;
        if (claimType == "preferred_username") factory.Backchannel.PreferredUsername = "different@example.test";
        else factory.Backchannel.Upn = "different@example.test";
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, token);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("consumed")]
    public async Task Expired_or_consumed_invitation_is_safely_rejected(string state)
    {
        await using var factory = new MockEntraFactory();
        var token = await factory.SeedInvitationAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invitation = await db.ExternalIdentityInvitations.SingleAsync();
            if (state == "expired") invitation.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            else { invitation.Status = "Consumed"; invitation.ConsumedAt = DateTimeOffset.UtcNow; }
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, token);
        Assert.Equal("/login?error=authentication.external_login_failed", callback.Headers.Location?.OriginalString);
        await using var verification = factory.Services.CreateAsyncScope();
        var verified = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await verified.ExternalIdentities.CountAsync());
        Assert.Equal(0, await verified.AuthenticationSessions.CountAsync());
    }

    [Fact]
    public async Task Concurrent_callbacks_consume_invitation_once_and_create_one_session()
    {
        await using var factory = new MockEntraFactory();
        var token = await factory.SeedInvitationAsync();
        using var first = factory.CreateOidcClient();
        using var second = factory.CreateOidcClient();
        var results = await Task.WhenAll(factory.AuthenticateAsync(first, token), factory.AuthenticateAsync(second, token));
        Assert.Single(results, item => item.Headers.Location?.OriginalString == "/");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.ExternalIdentities.CountAsync());
        Assert.Equal(1, await db.AuthenticationSessions.CountAsync());
        Assert.Equal("Consumed", await db.ExternalIdentityInvitations.Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task Failed_commit_rolls_back_link_and_never_issues_application_cookie()
    {
        await using var factory = new MockEntraFactory();
        var token = await factory.SeedInvitationAsync();
        factory.CommitFailure.FailAuthenticationCommit = true;
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, token);
        Assert.DoesNotContain(callback.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            value => value.Contains(ConvoLabAuthentication.SessionCookie, StringComparison.Ordinal));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.ExternalIdentities.CountAsync());
        Assert.Equal(0, await db.AuthenticationSessions.CountAsync());
        Assert.Equal("Active", await db.ExternalIdentityInvitations.Select(item => item.Status).SingleAsync());
        Assert.DoesNotContain(await db.WorkspaceAuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action is "Authentication.ExternalIdentityLinked" or "Authentication.ExternalIdentityInvitationConsumed"
            || item.Action == "Authentication.EntraLogin" && item.Outcome == "Succeeded");
        Assert.Empty(await db.AnalyticsOutbox.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-tenant")]
    [InlineData("invalid-issuer")]
    [InlineData("invalid-audience")]
    [InlineData("expired")]
    [InlineData("invalid-nonce")]
    [InlineData("invalid-state")]
    public async Task Invalid_or_unlinked_callbacks_are_safely_rejected(string scenario)
    {
        await using var factory = new MockEntraFactory();
        if (scenario != "unknown") await factory.SeedLinkedUserAsync(activeIdentity: true, activeUser: true);
        factory.Backchannel.Scenario = scenario;
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, tamperState: scenario == "invalid-state");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.StartsWith("/login?error=authentication.external_login_failed", callback.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Subject, await callback.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true, "authentication.external_identity_disabled")]
    [InlineData(true, false, "authentication.user_inactive")]
    public async Task Disabled_identity_or_inactive_user_is_rejected(bool identityActive, bool userActive, string expectedAuditCode)
    {
        await using var factory = new MockEntraFactory();
        await factory.SeedLinkedUserAsync(identityActive, userActive);
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/login?error=authentication.external_login_failed", callback.Headers.Location?.OriginalString);
        Assert.False(string.IsNullOrWhiteSpace(expectedAuditCode));
    }
}

internal sealed class MockEntraFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    public const string Tenant = "11111111-1111-1111-1111-111111111111";
    public const string ClientId = "22222222-2222-2222-2222-222222222222";
    public const string Authority = "https://stub-idp.test/11111111-1111-1111-1111-111111111111/v2.0";
    public static readonly Guid UserId = Guid.Parse("70000000-0000-0000-0000-000000000101");
    public static readonly Guid IdentityId = Guid.Parse("70000000-0000-0000-0000-000000000201");
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"convolab-oidc-{Guid.NewGuid():N}.db");
    private readonly string _email = $"approved-{Guid.NewGuid():N}@example.test";
    public StubOidcBackchannel Backchannel { get; } = new();
    public AuthenticationCommitFailureInterceptor CommitFailure { get; } = new();
    public string Subject => "stable-subject-1";
    public string InvitedEmail => _email;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={_database}",
            ["Database:Provider"] = "SQLite", ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Authentication:Mode"] = "Entra", ["Authentication:Local:Enabled"] = "false",
            ["Authentication:Entra:Enabled"] = "true", ["Authentication:Entra:Authority"] = Authority,
            ["Authentication:Entra:TenantId"] = Tenant, ["Authentication:Entra:ClientId"] = ClientId,
            ["Authentication:Entra:ClientSecretReference"] = "env:STUB_SECRET",
            ["Authentication:Entra:CallbackPath"] = "/signin-oidc",
            ["Authentication:Entra:SignedOutCallbackPath"] = "/signout-callback-oidc",
            ["Authentication:Entra:InvitationExpiryHours"] = "24"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_database}")
                .AddInterceptors(CommitFailure));
            services.RemoveAll<ISecretStore>();
            services.AddSingleton<ISecretStore, StubSecretStore>();
            services.PostConfigure<OpenIdConnectOptions>(EntraAuthentication.Scheme, options =>
            {
                Backchannel.Configure(options, Authority, ClientId, Tenant, _email);
            });
        });
    }

    public HttpClient CreateOidcClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    public async Task<HttpResponseMessage> AuthenticateAsync(HttpClient client, string? invitationToken = null, bool tamperState = false)
    {
        if (invitationToken is not null)
        {
            var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
            var antiforgery = JsonDocument.Parse(await antiforgeryResponse.Content.ReadAsStringAsync());
            using var prepare = new HttpRequestMessage(HttpMethod.Post, "/api/auth/entra/prepare-invitation")
            {
                Content = JsonContent.Create(new { token = invitationToken })
            };
            prepare.Headers.Add(ConvoLabAuthentication.AntiforgeryHeader,
                antiforgery.RootElement.GetProperty("token").GetString());
            var prepared = await client.SendAsync(prepare);
            Assert.Equal(HttpStatusCode.NoContent, prepared.StatusCode);
        }
        using var challenge = new HttpRequestMessage(HttpMethod.Get, "/api/auth/entra/login?returnUrl=%2F");
        var redirect = await client.SendAsync(challenge);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        var query = QueryHelpers.ParseQuery(redirect.Headers.Location!.Query);
        var code = $"deterministic-code-{Guid.NewGuid():N}";
        Backchannel.Register(code, query["nonce"].ToString());
        var state = query["state"].ToString();
        if (tamperState) state += "tampered";
        using var callback = new HttpRequestMessage(HttpMethod.Post, "/signin-oidc")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code, ["state"] = state
            })
        };
        return await client.SendAsync(callback);
    }

    public async Task SeedLinkedUserAsync(bool activeIdentity, bool activeUser)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(Path.GetFullPath(_database), Path.GetFullPath(db.Database.GetDbConnection().DataSource));
        var now = DateTimeOffset.UtcNow;
        var user = new IdentityUserRecord
        {
            Id = UserId, Email = _email, NormalizedEmail = _email.ToUpperInvariant(),
            DisplayName = "Approved user", Status = activeUser ? "Active" : "Disabled", CreatedAt = now, UpdatedAt = now
        };
        db.IdentityUsers.Add(user);
        db.ExternalIdentities.Add(new ExternalIdentityRecord
        {
            Id = IdentityId, UserId = user.Id, Provider = "Entra", Issuer = Authority,
            Subject = Subject, TenantId = Tenant, CreatedAt = now, LastLoginAt = now, IsActive = activeIdentity
        });
        db.WorkspaceMemberships.Add(new WorkspaceMembershipRecord
        {
            Id = Guid.NewGuid(), WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId, UserId = user.Id,
            Role = "Member", Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    public async Task<string> SeedInvitationAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(Path.GetFullPath(_database), Path.GetFullPath(db.Database.GetDbConnection().DataSource));
        var now = DateTimeOffset.UtcNow; var token = ConvoLabAuthentication.NewSecret();
        var user = new IdentityUserRecord
        {
            Id = Guid.NewGuid(), Email = _email, NormalizedEmail = _email.ToUpperInvariant(),
            DisplayName = "Invited user", Status = "Invited", CreatedAt = now, UpdatedAt = now
        };
        db.IdentityUsers.Add(user);
        db.ExternalIdentityInvitations.Add(new ExternalIdentityInvitationRecord
        {
            Id = Guid.NewGuid(), UserId = user.Id, InvitedEmail = user.Email, NormalizedEmail = user.NormalizedEmail,
            ExpectedTenant = Tenant, ExpectedProvider = "Entra", TokenHash = ConvoLabAuthentication.HashSecret(token),
            ExpiresAt = now.AddHours(1), CreatedBy = Guid.NewGuid(), CreatedAt = now
        });
        await db.SaveChangesAsync();
        return token;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_database)) File.Delete(_database);
    }
}

internal sealed class StubOidcBackchannel : HttpMessageHandler
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _nonces = new();
    public string Scenario { get; set; } = "valid";
    public bool IncludeEmail { get; set; } = true;
    public string? EmailOverride { get; set; }
    public string? PreferredUsername { get; set; }
    public string? Upn { get; set; }
    private string _issuer = string.Empty; private string _clientId = string.Empty; private string _tenant = string.Empty;
    private string _email = string.Empty;

    public void Configure(OpenIdConnectOptions options, string issuer, string clientId, string tenant, string email)
    {
        _issuer = issuer; _clientId = clientId; _tenant = tenant; _email = email;
        options.ClientId = clientId;
        options.Authority = issuer;
        options.TokenValidationParameters.ValidAudience = clientId;
        options.TokenValidationParameters.ValidIssuer = issuer;
        var key = new RsaSecurityKey(_rsa) { KeyId = "stub-key" };
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = issuer, AuthorizationEndpoint = "https://stub-idp.test/authorize",
            TokenEndpoint = "https://stub-idp.test/token", EndSessionEndpoint = "https://stub-idp.test/logout"
        };
        configuration.SigningKeys.Add(key);
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        options.Backchannel = new HttpClient(this, disposeHandler: false);
    }

    public void Register(string code, string nonce) => _nonces[code] = nonce;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var form = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var code = QueryHelpers.ParseQuery("?" + form)["code"].ToString();
        var now = DateTime.UtcNow;
        var issuer = Scenario == "invalid-issuer" ? "https://other-issuer.test/v2.0" : _issuer;
        var audience = Scenario == "invalid-audience" ? "wrong-client" : _clientId;
        var tenant = Scenario == "wrong-tenant" ? "33333333-3333-3333-3333-333333333333" : _tenant;
        var nonce = Scenario == "invalid-nonce" ? "wrong-nonce" : _nonces.GetValueOrDefault(code, "missing-nonce");
        var claims = new List<Claim>
        {
            new("sub", "stable-subject-1"), new("tid", tenant), new("nonce", nonce), new("name", "Approved user")
        };
        if (IncludeEmail) claims.Add(new Claim("email", EmailOverride ?? _email));
        if (!string.IsNullOrWhiteSpace(PreferredUsername)) claims.Add(new Claim("preferred_username", PreferredUsername));
        if (!string.IsNullOrWhiteSpace(Upn)) claims.Add(new Claim("upn", Upn));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer, Audience = audience,
            NotBefore = Scenario == "expired" ? now.AddHours(-2) : now.AddMinutes(-1),
            Expires = Scenario == "expired" ? now.AddHours(-1) : now.AddMinutes(10),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = "stub-key" }, SecurityAlgorithms.RsaSha256)
        };
        var token = new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
        var json = $$"""{"token_type":"Bearer","expires_in":600,"access_token":"stub-access-token","id_token":"{{token}}"}""";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}

internal sealed class AuthenticationCommitFailureInterceptor : DbTransactionInterceptor
{
    public bool FailAuthenticationCommit { get; set; }
    public bool ObserveAuthenticationCommit { get; set; }
    public bool AuthenticationCommitCompleted { get; private set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (FailAuthenticationCommit)
        {
            FailAuthenticationCommit = false;
            throw new DbUpdateException("Deterministic authentication commit failure.");
        }
        return ValueTask.FromResult(result);
    }

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (ObserveAuthenticationCommit) AuthenticationCommitCompleted = true;
        return Task.CompletedTask;
    }
}

internal sealed class StubSecretStore : ISecretStore
{
    public Task<SecretResolutionResult> ResolveAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(SecretResolutionResult.Resolved("stub", "not-a-real-secret"));
    public Task<SecretValidationResult> ValidateAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(new SecretValidationResult("stub", SecretResolutionStatus.Resolved));
    public void Invalidate(string reference) { }
}
