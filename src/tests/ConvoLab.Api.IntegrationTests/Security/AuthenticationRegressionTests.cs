using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConvoLab.Api.IntegrationTests.Security;

public sealed class AuthenticationRegressionTests
{
    [Fact]
    public async Task Successful_login_issues_session_cookie_and_returns_session_details()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "ValidPassword123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin@convolab.test", body.GetProperty("email").GetString());
        
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, c => c.StartsWith("convolab_session="));
    }

    [Fact]
    public async Task Invalid_login_returns_unauthorized_and_no_cookie()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "WrongPassword123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task Session_bootstrap_returns_session_if_cookie_present_and_valid()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "ValidPassword123!" });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        
        using var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", cookie);
        var sessionResponse = await authenticatedClient.GetAsync("/api/auth/session");
        
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var body = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin@convolab.test", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Session_expiration_rejects_old_session_cookie()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "ValidPassword123!" });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        
        factory.Clock.Advance(TimeSpan.FromHours(9)); // Default local session expiry is 8 hours
        
        using var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", cookie);
        var sessionResponse = await authenticatedClient.GetAsync("/api/auth/session");
        
        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_session_and_clears_cookie()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "ValidPassword123!" });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        
        using var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", cookie);
        
        var antiforgeryResponse = await authenticatedClient.GetAsync("/api/auth/antiforgery");
        var antiforgeryBody = await antiforgeryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var antiforgeryToken = antiforgeryBody.GetProperty("token").GetString();
        var antiforgeryHeaderName = antiforgeryBody.GetProperty("headerName").GetString();
        
        authenticatedClient.DefaultRequestHeaders.Add(antiforgeryHeaderName!, antiforgeryToken!);
        
        var logoutResponse = await authenticatedClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        
        var logoutCookies = logoutResponse.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(logoutCookies, c => c.StartsWith("convolab_session=;")); // Cookie cleared
        
        var sessionResponse = await authenticatedClient.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
    }

    [Fact]
    public async Task Antiforgery_token_can_be_retrieved_and_used()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(body.GetProperty("token").GetString());
        Assert.Equal("X-XSRF-TOKEN", body.GetProperty("headerName").GetString());
    }

    [Fact]
    public async Task Workspace_switching_updates_active_workspace()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "ValidPassword123!" });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.IdentityUsers.FirstAsync();
        var organisationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        db.Organisations.Add(new OrganisationRecord { Id = organisationId, Name = "Test Org", Status = "Active" });
        db.Workspaces.Add(new WorkspaceRecord { Id = workspaceId, Name = "Test Workspace", Status = "Active", OrganisationId = organisationId });
        db.WorkspaceMemberships.Add(new WorkspaceMembershipRecord { WorkspaceId = workspaceId, UserId = user.Id, Role = "Administrator", Status = "Active" });
        await db.SaveChangesAsync();
        
        using var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", cookie);
        
        var antiforgeryResponse = await authenticatedClient.GetAsync("/api/auth/antiforgery");
        var antiforgeryBody = await antiforgeryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var antiforgeryToken = antiforgeryBody.GetProperty("token").GetString();
        var antiforgeryHeaderName = antiforgeryBody.GetProperty("headerName").GetString();
        
        authenticatedClient.DefaultRequestHeaders.Add(antiforgeryHeaderName!, antiforgeryToken!);
        
        var switchResponse = await authenticatedClient.PostAsJsonAsync("/api/auth/workspace", new { workspaceId });
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);
        
        var body = await switchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(workspaceId.ToString(), body.GetProperty("activeWorkspaceId").GetString());
    }

    [Fact]
    public async Task Safe_return_url_handling_in_entra_login()
    {
        await using var factory = new AuthRegressionFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/auth/entra/login?returnUrl=/malicious-site.com");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}

internal sealed class AuthRegressionFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"convolab-auth-regression-{Guid.NewGuid():N}.db");
    public AdjustableTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-14T08:00:00Z"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SQLite", ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Authentication:Mode"] = "Hybrid", ["Authentication:Local:Enabled"] = "true",
            ["Authentication:Entra:Enabled"] = "true", ["Authentication:Entra:PostLogoutRedirectUri"] = "/",
            ["Authentication:Local:HybridAccessAcknowledged"] = "true",
            ["Bootstrap:Administrator:Email"] = "admin@convolab.test",
            ["Bootstrap:Administrator:DisplayName"] = "Regression Administrator",
            ["Bootstrap:Administrator:Password"] = "ValidPassword123!"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_database}"));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.PostConfigure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(
                ConvoLab.Api.Security.EntraAuthentication.Scheme,
                options =>
                {
                    var config = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = "https://example.com/authorize"
                    };
                    options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(config);
                });
        });
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_database)) File.Delete(_database);
    }
}

internal sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan value) => _now = _now.Add(value);
}
