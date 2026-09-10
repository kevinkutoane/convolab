using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ConvoLab.Infrastructure.Data;

namespace ConvoLab.Api.IntegrationTests.Security;

/// <summary>
/// Verifies that the <see cref="ConvoLab.Api.Middleware.SecurityHeadersMiddleware"/>
/// emits the required hardened HTTP response headers on every API response, regardless
/// of authentication status or response status code.
/// </summary>
public sealed class SecurityHeadersTests
{
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    [InlineData("Cross-Origin-Opener-Policy", "same-origin")]
    [InlineData("Cross-Origin-Embedder-Policy", "require-corp")]
    [InlineData("X-Permitted-Cross-Domain-Policies", "none")]
    public async Task Required_security_header_is_present_on_anonymous_health_endpoint(string header, string expectedValue)
    {
        await using var factory = new SecurityHeadersFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        var actualValue = response.Headers.TryGetValues(header, out var values)
            ? string.Join(", ", values)
            : null;

        Assert.True(
            actualValue != null,
            $"Expected header '{header}' to be present but it was absent.");
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public async Task Content_Security_Policy_header_is_present_and_contains_self_default_src()
    {
        await using var factory = new SecurityHeadersFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var csp = response.Headers.TryGetValues("Content-Security-Policy", out var values)
            ? string.Join(" ", values)
            : null;

        Assert.NotNull(csp);
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.DoesNotContain("'unsafe-eval'", csp);
    }

    [Fact]
    public async Task Permissions_Policy_header_restricts_camera_microphone_geolocation()
    {
        await using var factory = new SecurityHeadersFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var policy = response.Headers.TryGetValues("Permissions-Policy", out var values)
            ? string.Join(", ", values)
            : null;

        Assert.NotNull(policy);
        Assert.Contains("camera=()", policy);
        Assert.Contains("microphone=()", policy);
        Assert.Contains("geolocation=()", policy);
    }

    [Fact]
    public async Task Security_headers_are_present_on_401_unauthenticated_api_response()
    {
        await using var factory = new SecurityHeadersFactory();
        using var client = factory.CreateClient();

        // Hit a protected endpoint without authentication
        var response = await client.GetAsync("/api/audit/events");

        // We don't assert 401 here because the test factory is non-Production;
        // we only assert headers are present regardless of status.
        Assert.True(
            response.Headers.Contains("X-Content-Type-Options"),
            "X-Content-Type-Options header must be present on all API responses.");
        Assert.True(
            response.Headers.Contains("X-Frame-Options"),
            "X-Frame-Options header must be present on all API responses.");
    }

    [Fact]
    public async Task HSTS_header_is_absent_in_non_production_environment()
    {
        // UseHsts() and the SecurityHeadersMiddleware both suppress HSTS in non-Production.
        await using var factory = new SecurityHeadersFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        var hasHsts = response.Headers.Contains("Strict-Transport-Security");
        Assert.False(hasHsts, "HSTS must not be emitted in non-Production environments.");
    }
}

internal sealed class SecurityHeadersFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"convolab-security-headers-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SQLite", ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Authentication:Mode"] = "Local", ["Authentication:Local:Enabled"] = "true",
            ["Bootstrap:Administrator:Email"] = "admin@headers.test",
            ["Bootstrap:Administrator:DisplayName"] = "Header Test Admin",
            ["Bootstrap:Administrator:Password"] = "ValidPassword123!"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_database}"));
        });
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_database)) File.Delete(_database);
    }
}
