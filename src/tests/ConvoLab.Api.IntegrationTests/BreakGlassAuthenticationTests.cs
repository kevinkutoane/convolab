using System.Net;
using System.Net.Http.Json;
using System.Diagnostics.Metrics;
using System.Text.Json;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConvoLab.Api.IntegrationTests;

public sealed class BreakGlassAuthenticationTests
{
    [Fact]
    public async Task Failures_increment_dedicated_state_and_lock_without_touching_local_lockout()
    {
        await using var factory = new BreakGlassFactory(rateLimit: 60);
        using var client = factory.CreateClient();
        for (var index = 0; index < 5; index++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await FailAsync(client)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var credential = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .LocalCredentials.AsNoTracking().SingleAsync();
        Assert.Equal(5, credential.BreakGlassFailedAttempts);
        Assert.NotNull(credential.BreakGlassLockedUntil);
        Assert.NotNull(credential.BreakGlassLastFailedAt);
        Assert.Equal(0, credential.FailedAttempts);
        Assert.Null(credential.LockedUntil);
    }

    [Fact]
    public async Task Correct_password_is_denied_while_locked_then_succeeds_after_deterministic_expiry()
    {
        await using var factory = new BreakGlassFactory(rateLimit: 60);
        using var client = factory.CreateClient();
        for (var index = 0; index < 5; index++) await FailAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SucceedAsync(client, breakGlass: true)).StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.OK, (await SucceedAsync(client, breakGlass: true)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var credential = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .LocalCredentials.AsNoTracking().SingleAsync();
        Assert.Equal(0, credential.BreakGlassFailedAttempts);
        Assert.Null(credential.BreakGlassLockedUntil);
    }

    [Fact]
    public async Task Successful_break_glass_resets_subthreshold_failures()
    {
        await using var factory = new BreakGlassFactory(rateLimit: 60);
        using var client = factory.CreateClient();
        await FailAsync(client); await FailAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await SucceedAsync(client, breakGlass: true)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .LocalCredentials.Select(item => item.BreakGlassFailedAttempts).SingleAsync());
    }

    [Fact]
    public async Task Dedicated_limiter_does_not_exhaust_ordinary_login_policy()
    {
        await using var factory = new BreakGlassFactory(rateLimit: 3);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await FailAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await FailAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await FailAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await FailAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SucceedAsync(client, breakGlass: false)).StatusCode);
    }

    [Fact]
    public async Task Generic_problem_and_persisted_evidence_do_not_contain_credential_sentinels()
    {
        await using var factory = new BreakGlassFactory(rateLimit: 60);
        using var client = factory.CreateClient();
        var response = await FailAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("authentication.break_glass_denied", problem.RootElement.GetProperty("code").GetString());
        var publicPayload = problem.RootElement.GetRawText();
        Assert.DoesNotContain("admin@breakglass.test", publicPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Wrong-Sentinel-Password!", publicPayload, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audits = await db.WorkspaceAuditEvents.AsNoTracking()
            .Where(item => item.Action.StartsWith("Authentication.BreakGlass"))
            .Select(item => new { item.Action, item.ActorDisplay, item.DetailJson, item.ResourceId })
            .ToListAsync();
        Assert.Contains(audits, item => item.Action == "Authentication.BreakGlassFailure");
        var evidence = JsonSerializer.Serialize(audits);
        Assert.DoesNotContain("admin@breakglass.test", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Wrong-Sentinel-Password!", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_failures_preserve_the_account_level_count()
    {
        await using var factory = new BreakGlassFactory(rateLimit: 60);
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => FailAsync(client)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .LocalCredentials.Select(item => item.BreakGlassFailedAttempts).SingleAsync());
    }

    [Fact]
    public async Task Metric_uses_only_bounded_non_identifying_labels()
    {
        var measurements = new System.Collections.Concurrent.ConcurrentBag<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ConvoLabTelemetry.MeterName
                && instrument.Name == "convolab.auth.break_glass.total")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Add(tags.ToArray()));
        listener.Start();
        await using var factory = new BreakGlassFactory(rateLimit: 60);
        using var client = factory.CreateClient();
        await FailAsync(client);

        var labels = Assert.Single(measurements);
        Assert.Equal(["failure_code", "lockout_state", "outcome"],
            labels.Select(item => item.Key).OrderBy(item => item).ToArray());
        Assert.Contains(labels, item => item is { Key: "outcome", Value: "denied" });
        Assert.Contains(labels, item => item is { Key: "failure_code", Value: "invalid_credentials" });
        Assert.Contains(labels, item => item is { Key: "lockout_state", Value: "unlocked" });
        Assert.DoesNotContain("admin@breakglass.test", JsonSerializer.Serialize(labels), StringComparison.OrdinalIgnoreCase);
    }

    private static Task<HttpResponseMessage> FailAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/break-glass/login", new { email = "admin@breakglass.test", password = "Wrong-Sentinel-Password!" });

    private static Task<HttpResponseMessage> SucceedAsync(HttpClient client, bool breakGlass) =>
        client.PostAsJsonAsync(breakGlass ? "/api/auth/break-glass/login" : "/api/auth/login",
            new { email = "admin@breakglass.test", password = "Ephemeral-BreakGlass-Alpha14!" });
}

internal sealed class BreakGlassFactory(int rateLimit) : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"convolab-break-glass-{Guid.NewGuid():N}.db");
    public AdjustableTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-14T08:00:00Z"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Local:BreakGlass:RateLimitPerMinute", rateLimit.ToString());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SQLite", ["Database:ApplyMigrationsOnStartup"] = "true",
            ["Authentication:Mode"] = "Hybrid", ["Authentication:Local:Enabled"] = "true",
            ["Authentication:Local:HybridAccessAcknowledged"] = "true",
            ["Authentication:Local:BreakGlassEnabled"] = "true",
            ["Authentication:Local:BreakGlassAccountConfigured"] = "true",
            ["Authentication:Local:BreakGlass:MaximumAttempts"] = "5",
            ["Authentication:Local:BreakGlass:LockoutMinutes"] = "15",
            ["Authentication:Local:BreakGlass:RateLimitPerMinute"] = rateLimit.ToString(),
            ["Bootstrap:Administrator:Email"] = "admin@breakglass.test",
            ["Bootstrap:Administrator:DisplayName"] = "Emergency administrator",
            ["Bootstrap:Administrator:Password"] = "Ephemeral-BreakGlass-Alpha14!"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={_database}"));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
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
