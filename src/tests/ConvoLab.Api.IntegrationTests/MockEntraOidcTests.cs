using System.IdentityModel.Tokens.Jwt;
using System.Collections.Concurrent;
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
using Microsoft.Extensions.Logging;
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
        await AssertSingleFailedLoginEvidenceAsync(db, "authentication.invitation_email_mismatch");
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
        await AssertSingleFailedLoginEvidenceAsync(db, "authentication.invitation_consumed");
    }

    [Theory]
    [InlineData("unknown", "authentication.external_identity_not_linked")]
    [InlineData("wrong-tenant", "authentication.entra.claims_invalid")]
    [InlineData("invalid-issuer", "authentication.entra.remote_failure")]
    [InlineData("invalid-audience", "authentication.entra.remote_failure")]
    [InlineData("expired", "authentication.entra.remote_failure")]
    [InlineData("invalid-nonce", "authentication.entra.remote_failure")]
    [InlineData("invalid-state", "authentication.entra.remote_failure")]
    [InlineData("token-exchange-failure", "authentication.entra.remote_failure")]
    public async Task Invalid_or_unlinked_callbacks_persist_exactly_one_safe_failure(
        string scenario,
        string expectedFailureCode)
    {
        await using var factory = new MockEntraFactory();
        if (scenario != "unknown") await factory.SeedLinkedUserAsync(activeIdentity: true, activeUser: true);
        factory.Backchannel.Scenario = scenario;
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client, tamperState: scenario == "invalid-state");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.StartsWith("/login?error=authentication.external_login_failed", callback.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Subject, await callback.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await AssertSingleFailedLoginEvidenceAsync(db, expectedFailureCode);
        Assert.Equal(0, await db.AuthenticationSessions.CountAsync());
        Assert.DoesNotContain(await db.WorkspaceAuditEvents.AsNoTracking().ToListAsync(), item =>
            item.Action == "Authentication.EntraLogin" && item.Outcome == "Succeeded");
    }

    [Fact]
    public async Task Unavailable_client_authentication_persists_one_bounded_failure()
    {
        await using var factory = new MockEntraFactory();
        await factory.SeedLinkedUserAsync(activeIdentity: true, activeUser: true);
        factory.SecretStore.IsAvailable = false;
        using var client = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        await AssertSingleFailedLoginEvidenceAsync(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            "authentication.entra.client_authentication_unavailable");
    }

    [Fact]
    public async Task Framework_failure_is_included_in_sanitized_Operations_failure_count()
    {
        await using var factory = new MockEntraFactory();
        await factory.SeedLinkedUserAsync(activeIdentity: true, activeUser: true, platformAdministrator: true);
        factory.Backchannel.Scenario = "invalid-issuer";
        using (var failureClient = factory.CreateOidcClient())
            Assert.Equal(HttpStatusCode.Redirect, (await factory.AuthenticateAsync(failureClient)).StatusCode);

        factory.Backchannel.Scenario = "valid";
        using var administratorClient = factory.CreateOidcClient();
        Assert.Equal("/", (await factory.AuthenticateAsync(administratorClient)).Headers.Location?.OriginalString);
        var operations = await administratorClient.GetAsync("/api/operations/authentication");
        Assert.Equal(HttpStatusCode.OK, operations.StatusCode);
        using var payload = JsonDocument.Parse(await operations.Content.ReadAsStringAsync());
        Assert.Equal(1, payload.RootElement.GetProperty("externalLoginFailuresLast24Hours").GetInt32());
    }

    [Fact]
    public async Task Remote_failure_evidence_and_surfaces_exclude_raw_Oidc_sentinels()
    {
        await using var factory = new MockEntraFactory();
        await factory.SeedLinkedUserAsync(activeIdentity: true, activeUser: true, platformAdministrator: true);
        factory.Backchannel.Scenario = "invalid-nonce";
        factory.Backchannel.Subject = "subject-sentinel-never-persist";
        factory.Backchannel.AccessToken = "access-token-sentinel-never-persist";
        factory.Backchannel.InvalidNonce = "nonce-sentinel-never-persist";
        factory.Backchannel.EmailOverride = "email-sentinel@example.test";
        factory.SecretStore.SecretValue = "client-secret-sentinel-never-persist";
        using var failureClient = factory.CreateOidcClient();
        var callback = await factory.AuthenticateAsync(
            failureClient,
            authorizationCode: "authorization-code-sentinel-never-persist");
        var failedIdToken = factory.Backchannel.LastIdToken;

        using var stateFailureClient = factory.CreateOidcClient();
        var stateCallback = await factory.AuthenticateAsync(
            stateFailureClient,
            authorizationCode: "state-code-sentinel-never-persist",
            stateTamperSuffix: "state-sentinel-never-persist");

        factory.Backchannel.Scenario = "valid";
        factory.Backchannel.Subject = factory.Subject;
        factory.Backchannel.EmailOverride = factory.InvitedEmail;
        using var administratorClient = factory.CreateOidcClient();
        await factory.AuthenticateAsync(administratorClient);
        var operations = await administratorClient.GetAsync("/api/operations/authentication");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evidence = string.Join('\n',
            await db.WorkspaceAuditEvents.AsNoTracking()
                .Where(item => item.Action == "Authentication.EntraLogin" && item.Outcome == "Failed")
                .Select(item => item.DetailJson).ToListAsync())
            + string.Join('\n', await db.AnalyticsOutbox.AsNoTracking().Select(item => item.PayloadJson).ToListAsync())
            + await callback.Content.ReadAsStringAsync()
            + await stateCallback.Content.ReadAsStringAsync()
            + callback.Headers.Location?.OriginalString
            + stateCallback.Headers.Location?.OriginalString
            + await operations.Content.ReadAsStringAsync()
            + string.Join('\n', factory.Logs.Entries);
        var sentinels = new[]
        {
            failedIdToken,
            "access-token-sentinel-never-persist",
            "authorization-code-sentinel-never-persist",
            "state-code-sentinel-never-persist",
            "state-sentinel-never-persist",
            "nonce-sentinel-never-persist",
            "subject-sentinel-never-persist",
            "email-sentinel@example.test",
            MockEntraFactory.Tenant,
            MockEntraFactory.Authority,
            "client-secret-sentinel-never-persist",
            "env:STUB_SECRET"
        };
        Assert.All(sentinels.Where(item => !string.IsNullOrWhiteSpace(item)), sentinel =>
            Assert.DoesNotContain(sentinel!, evidence, StringComparison.OrdinalIgnoreCase));
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
        await using var scope = factory.Services.CreateAsyncScope();
        await AssertSingleFailedLoginEvidenceAsync(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            expectedAuditCode);
    }

    private static async Task AssertSingleFailedLoginEvidenceAsync(ApplicationDbContext db, string expectedFailureCode)
    {
        var audit = Assert.Single(await db.WorkspaceAuditEvents.AsNoTracking()
            .Where(item => item.Action == "Authentication.EntraLogin" && item.Outcome == "Failed")
            .ToListAsync());
        using (var detail = JsonDocument.Parse(audit.DetailJson))
            Assert.Equal(expectedFailureCode, detail.RootElement.GetProperty("failureCode").GetString());

        var outbox = Assert.Single(
            await db.AnalyticsOutbox.AsNoTracking().ToListAsync(),
            item => item.PayloadJson.Contains("\"eventType\":\"UserLoginFailed\"", StringComparison.Ordinal));
        using var payload = JsonDocument.Parse(outbox.PayloadJson);
        Assert.Equal("UserLoginFailed", payload.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("Failed", payload.RootElement.GetProperty("outcome").GetString());
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
    public StubSecretStore SecretStore { get; } = new();
    public CollectingLoggerProvider Logs { get; } = new();
    public AuthenticationCommitFailureInterceptor CommitFailure { get; } = new();
    public string Subject => "stable-subject-1";
    public string InvitedEmail => _email;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
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
            services.AddSingleton<ISecretStore>(SecretStore);
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

    public async Task<HttpResponseMessage> AuthenticateAsync(
        HttpClient client,
        string? invitationToken = null,
        bool tamperState = false,
        string? authorizationCode = null,
        string? stateTamperSuffix = null)
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
        var code = authorizationCode ?? $"deterministic-code-{Guid.NewGuid():N}";
        Backchannel.Register(code, query["nonce"].ToString());
        var state = query["state"].ToString();
        if (tamperState || stateTamperSuffix is not null) state += stateTamperSuffix ?? "tampered";
        using var callback = new HttpRequestMessage(HttpMethod.Post, "/signin-oidc")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code, ["state"] = state
            })
        };
        return await client.SendAsync(callback);
    }

    public async Task SeedLinkedUserAsync(bool activeIdentity, bool activeUser, bool platformAdministrator = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(Path.GetFullPath(_database), Path.GetFullPath(db.Database.GetDbConnection().DataSource));
        var now = DateTimeOffset.UtcNow;
        var user = new IdentityUserRecord
        {
            Id = UserId, Email = _email, NormalizedEmail = _email.ToUpperInvariant(),
            DisplayName = "Approved user", Status = activeUser ? "Active" : "Disabled",
            IsPlatformAdministrator = platformAdministrator, CreatedAt = now, UpdatedAt = now
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
    public string Subject { get; set; } = "stable-subject-1";
    public string AccessToken { get; set; } = "stub-access-token";
    public string InvalidNonce { get; set; } = "wrong-nonce";
    public string? LastIdToken { get; private set; }
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
        if (Scenario == "token-exchange-failure")
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json")
            };
        var now = DateTime.UtcNow;
        var issuer = Scenario == "invalid-issuer" ? "https://other-issuer.test/v2.0" : _issuer;
        var audience = Scenario == "invalid-audience" ? "wrong-client" : _clientId;
        var tenant = Scenario == "wrong-tenant" ? "33333333-3333-3333-3333-333333333333" : _tenant;
        var nonce = Scenario == "invalid-nonce" ? InvalidNonce : _nonces.GetValueOrDefault(code, "missing-nonce");
        var claims = new List<Claim>
        {
            new("sub", Subject), new("tid", tenant), new("nonce", nonce), new("name", "Approved user")
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
        LastIdToken = token;
        var json = $$"""{"token_type":"Bearer","expires_in":600,"access_token":"{{AccessToken}}","id_token":"{{token}}"}""";
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
    public bool IsAvailable { get; set; } = true;
    public string SecretValue { get; set; } = "not-a-real-secret";

    public Task<SecretResolutionResult> ResolveAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(IsAvailable
            ? SecretResolutionResult.Resolved("stub", SecretValue)
            : SecretResolutionResult.Failed(
                "stub",
                SecretResolutionStatus.Unavailable,
                "secret.stub.unavailable"));
    public Task<SecretValidationResult> ValidateAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(new SecretValidationResult("stub", SecretResolutionStatus.Resolved));
    public void Invalidate(string reference) { }
    public void Clear() { }
}

internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();
    public IReadOnlyCollection<string> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(_entries);
    public void Dispose() { }

    private sealed class CollectingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(exception is null
                ? formatter(state, null)
                : $"{formatter(state, exception)} {exception}");
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
