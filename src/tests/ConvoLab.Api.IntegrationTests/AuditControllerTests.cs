using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConvoLab.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="ConvoLab.Api.Controllers.AuditController"/>.
/// Verifies that all audit endpoints require PlatformAdministrator authorization,
/// return correctly shaped paginated responses, and that the export endpoint
/// respects SafeMode:BlockAuditExports.
/// </summary>
public sealed class AuditControllerTests
{
    [Fact]
    public async Task Get_audit_events_requires_authentication()
    {
        await using var factory = new AuditControllerFactory();
        using var client = factory.CreateClient();

        // Pass invalid authorization to ensure unauthenticated rejection rather than fallback Testing principal
        client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid");

        var response = await client.GetAsync("/api/audit/events");

        // Unauthenticated request must be rejected.
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Expected 401 or redirect, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Get_audit_summary_returns_ok_for_platform_administrator()
    {
        await using var factory = new AuditControllerFactory();
        using var adminClient = await factory.CreateAdminClientAsync();

        var response = await adminClient.GetAsync("/api/audit/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("total", out _), "Response must contain 'total' field.");
        Assert.True(body.TryGetProperty("breakdown", out _), "Response must contain 'breakdown' field.");
    }

    [Fact]
    public async Task Get_audit_events_returns_paginated_response()
    {
        await using var factory = new AuditControllerFactory();
        using var adminClient = await factory.CreateAdminClientAsync();

        var response = await adminClient.GetAsync("/api/audit/events?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("events", out var events), "Response must contain 'events' array.");
        Assert.Equal(JsonValueKind.Array, events.ValueKind);
        Assert.True(body.TryGetProperty("total", out _));
        Assert.True(body.TryGetProperty("page", out _));
        Assert.True(body.TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task Get_audit_event_by_unknown_id_returns_not_found()
    {
        await using var factory = new AuditControllerFactory();
        using var adminClient = await factory.CreateAdminClientAsync();

        var response = await adminClient.GetAsync($"/api/audit/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Audit_export_is_blocked_when_safe_mode_flag_is_true()
    {
        await using var factory = new AuditControllerFactory(blockAuditExports: true);
        using var adminClient = await factory.CreateAdminClientAsync();

        var response = await adminClient.GetAsync("/api/audit/export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Audit_export_is_allowed_when_safe_mode_flag_is_false()
    {
        await using var factory = new AuditControllerFactory(blockAuditExports: false);
        using var adminClient = await factory.CreateAdminClientAsync();

        var response = await adminClient.GetAsync("/api/audit/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("events", out _), "Export response must contain 'events' array.");
        Assert.True(body.TryGetProperty("exportedAt", out _), "Export response must contain 'exportedAt'.");
    }

    [Fact]
    public async Task Audit_events_response_contains_no_sensitive_fields()
    {
        await using var factory = new AuditControllerFactory();
        using var adminClient = await factory.CreateAdminClientAsync();

        var response = await adminClient.GetAsync("/api/audit/events");
        var raw = await response.Content.ReadAsStringAsync();

        // Verify forbidden sensitive fields are absent from the response body
        Assert.DoesNotContain("\"password\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"token\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"secret\"", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"actorDisplay\"", raw, StringComparison.OrdinalIgnoreCase); // raw email not in DTO
    }
}

internal sealed class AuditControllerFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"convolab-audit-{Guid.NewGuid():N}.db");
    private readonly bool _blockAuditExports;

    public AuditControllerFactory(bool blockAuditExports = false)
    {
        _blockAuditExports = blockAuditExports;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SQLite", ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Authentication:Mode"] = "Local", ["Authentication:Local:Enabled"] = "true",
            ["Bootstrap:Administrator:Email"] = "admin@audit.test",
            ["Bootstrap:Administrator:DisplayName"] = "Audit Test Admin",
            ["Bootstrap:Administrator:Password"] = "ValidPassword123!",
            ["SafeMode:BlockAuditExports"] = _blockAuditExports ? "true" : "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_database}"));
        });
    }

    /// <summary>Authenticates as the bootstrap Platform Administrator and returns a configured client.</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Obtain antiforgery token
        var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
        antiforgeryResponse.EnsureSuccessStatusCode();
        var antiforgeryBody = await antiforgeryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = antiforgeryBody.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);

        // Login
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@audit.test", password = "ValidPassword123!" });
        loginResponse.EnsureSuccessStatusCode();

        if (loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var sessionCookie = cookies.FirstOrDefault(c => c.StartsWith("convolab_session="));
            if (sessionCookie != null)
            {
                var cookieValue = sessionCookie.Split(';')[0];
                client.DefaultRequestHeaders.Add("Cookie", cookieValue);
            }
        }

        return client;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_database)) File.Delete(_database);
    }
}
