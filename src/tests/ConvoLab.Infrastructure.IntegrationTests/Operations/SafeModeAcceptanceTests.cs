using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Operations;
using ConvoLab.Application.PluginStudio;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Intelligence;
using ConvoLab.Infrastructure.Operations;
using ConvoLab.Infrastructure.PluginStudio;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

[Collection("Environment variable isolation")]
public sealed class SafeModeAcceptanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Persisted_safe_mode_enforces_external_deterministic_and_export_policy_branches(
        bool allowDeterministic,
        bool blockExports)
    {
        await using var harness = await SafeModeHarness.CreateAsync(
            allowDeterministic, blockExports, persistedEnabled: true);

        var state = await harness.Service.GetAsync();
        Assert.True(state.PersistedSafeModeEnabled);
        Assert.True(state.EffectiveSafeModeEnabled);
        await AssertBlockedAsync(() => harness.Service.EnsureExternalExecutionAllowedAsync());

        if (allowDeterministic)
            await harness.Service.EnsureDeterministicExecutionAllowedAsync();
        else
            await AssertBlockedAsync(() => harness.Service.EnsureDeterministicExecutionAllowedAsync());

        if (blockExports)
            await AssertBlockedAsync(() => harness.Service.EnsureAnalyticsExportAllowedAsync());
        else
            await harness.Service.EnsureAnalyticsExportAllowedAsync();
    }

    [Fact]
    public async Task Safe_mode_blocks_plugin_probe_and_both_external_and_disallowed_deterministic_execution()
    {
        await using var harness = await SafeModeHarness.CreateAsync(
            allowDeterministic: false,
            blockExports: true,
            persistedEnabled: true);
        var probe = new HttpPluginHealthProbe(null!, harness.Service);
        await AssertBlockedAsync(() => probe.ProbeAsync(new PluginProbeRequest(
            "external-plugin",
            "https://plugins.example.test/manifest.json",
            "ExternalPlugin",
            PluginCategory.Tool)));

        var gemini = new GeminiIntelligenceExecutor(null!, null!, harness.Service);
        var routing = new RoutingIntelligenceExecutor(
            new DeterministicIntelligenceExecutor(), gemini, harness.Service);
        await AssertBlockedAsync(() => routing.ExecuteAsync(
            null!, "[PROVIDER:Gemini] external replay execution"));
        await AssertBlockedAsync(() => routing.ExecuteAsync(
            null!, "deterministic replay execution"));
    }

    [Fact]
    public async Task Safe_mode_mutation_persists_audit_analytics_and_warning_and_rejects_stale_revision()
    {
        await using var harness = await SafeModeHarness.CreateAsync(
            allowDeterministic: true,
            blockExports: false,
            persistedEnabled: false,
            seedEvidenceScope: true);
        var actorId = Guid.NewGuid();
        var activated = await harness.Service.UpdateSafeModeAsync(new UpdateSafeModeCommand(
            true,
            1,
            "Correction sprint activation evidence",
            "ACTIVATE SAFE MODE",
            actorId,
            "Safe mode acceptance",
            null,
            null,
            "safe-mode-correlation"));

        Assert.True(activated.EffectiveSafeModeEnabled);
        Assert.Equal(2, activated.Revision);
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Single(await db.WorkspaceAuditEvents.AsNoTracking()
                .Where(item => item.Action == "SafeMode.Activated")
                .ToListAsync());
            Assert.Single(await db.AnalyticsOutbox.AsNoTracking()
                .Where(item => item.PayloadJson.Contains("SafeMode.Activated"))
                .ToListAsync());
        }
        Assert.Contains(harness.Logger.Entries, entry =>
            entry.Level >= LogLevel.Warning
            && entry.Message.Contains("Safe mode changed", StringComparison.Ordinal));

        var conflict = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            harness.Service.UpdateSafeModeAsync(new UpdateSafeModeCommand(
                false,
                1,
                "Stale revision correction evidence",
                "DEACTIVATE SAFE MODE",
                actorId,
                "Safe mode acceptance",
                null,
                null,
                "safe-mode-stale-correlation")));
        Assert.Equal("revision.conflict", conflict.Code);
    }

    [Fact]
    public async Task Environment_override_is_effective_and_cannot_be_deactivated_by_api_mutation()
    {
        var prior = Environment.GetEnvironmentVariable("CONVOLAB_SAFE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("CONVOLAB_SAFE_MODE", "true");
            await using var harness = await SafeModeHarness.CreateAsync(
                allowDeterministic: true,
                blockExports: false,
                persistedEnabled: false);

            var state = await harness.Service.GetAsync();
            Assert.False(state.PersistedSafeModeEnabled);
            Assert.True(state.EnvironmentOverrideEnabled);
            Assert.True(state.EffectiveSafeModeEnabled);
            var conflict = await Assert.ThrowsAsync<ResourceConflictException>(() =>
                harness.Service.UpdateSafeModeAsync(new UpdateSafeModeCommand(
                    false,
                    state.Revision,
                    "Attempt to clear environment override",
                    "DEACTIVATE SAFE MODE",
                    Guid.NewGuid(),
                    "Safe mode acceptance",
                    null,
                    null,
                    "safe-mode-override-correlation")));
            Assert.Equal("safe_mode.override_active", conflict.Code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONVOLAB_SAFE_MODE", prior);
        }
    }

    private static async Task AssertBlockedAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<CapabilityUnavailableException>(action);
        Assert.Equal("operations.safe_mode_active", exception.Code);
    }

    private sealed class SafeModeHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ServiceProvider Services { get; }
        public PlatformOperationalStateService Service { get; }
        public CapturingLogger<PlatformOperationalStateService> Logger { get; }

        private SafeModeHarness(
            SqliteConnection connection,
            ServiceProvider services,
            PlatformOperationalStateService service,
            CapturingLogger<PlatformOperationalStateService> logger)
        {
            _connection = connection;
            Services = services;
            Service = service;
            Logger = logger;
        }

        public static async Task<SafeModeHarness> CreateAsync(
            bool allowDeterministic,
            bool blockExports,
            bool persistedEnabled,
            bool seedEvidenceScope = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection()
                .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection))
                .BuildServiceProvider();
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
                var record = await db.PlatformOperationalSettings.SingleAsync();
                record.SafeModeEnabled = persistedEnabled;
                record.SafeModeReason = persistedEnabled ? "Persisted acceptance state" : null;
                if (seedEvidenceScope)
                {
                    var organisationId = Guid.NewGuid();
                    var workspaceId = Guid.NewGuid();
                    var actorId = Guid.NewGuid();
                    var now = DateTimeOffset.UtcNow;
                    db.Organisations.Add(new OrganisationRecord
                    {
                        Id = organisationId,
                        Name = "Safe mode organisation",
                        Slug = $"safe-mode-{organisationId:N}",
                        Status = "Active",
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    db.Workspaces.Add(new WorkspaceRecord
                    {
                        Id = workspaceId,
                        OrganisationId = organisationId,
                        Name = "Safe mode workspace",
                        Slug = $"safe-mode-{workspaceId:N}",
                        Description = "Safe mode evidence acceptance",
                        Status = "Active",
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    db.RuntimeEnvironments.Add(new ConvoLab.Infrastructure.Settings.RuntimeEnvironmentRecord
                    {
                        Id = Guid.NewGuid(),
                        OrganisationId = organisationId,
                        WorkspaceId = workspaceId,
                        Name = "Safe mode environment",
                        Slug = $"safe-mode-environment-{workspaceId:N}",
                        EnvironmentType = "Development",
                        Description = "Safe mode evidence acceptance",
                        Status = "Active",
                        IsDefault = true,
                        CreatedAt = now,
                        CreatedBy = actorId,
                        UpdatedAt = now,
                        Revision = 1
                    });
                }
                await db.SaveChangesAsync();
            }
            var logger = new CapturingLogger<PlatformOperationalStateService>();
            var service = new PlatformOperationalStateService(
                services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new SafeModeOptions
                {
                    AllowDeterministicVerification = allowDeterministic,
                    BlockAnalyticsExports = blockExports
                }),
                logger);
            return new SafeModeHarness(connection, services, service, logger);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    public sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

[CollectionDefinition("Environment variable isolation", DisableParallelization = true)]
public sealed class EnvironmentVariableIsolationCollection;
