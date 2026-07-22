using ConvoLab.Application.PluginStudio;
using ConvoLab.Domain.Plugins.Enums;

namespace ConvoLab.Application.Tests.PluginStudio;

public sealed class PluginStudioServiceTests
{
    [Fact]
    public async Task Activation_records_health_evidence_and_exposes_plugin_to_runtime()
    {
        var repository = new InMemoryPluginRepository([Plugin(health: PluginHealthStatus.Unknown)]);
        var service = new PluginStudioService(repository, new FakeProbe(PluginHealthStatus.Healthy));
        var plugin = (await repository.ListPluginsAsync()).Single();

        var activated = await service.TransitionAsync(plugin.Id, "activate");
        var runtime = await service.GetActivePluginsAsync();

        Assert.Equal(PluginStatus.Active, activated.Summary.Status);
        Assert.Equal(PluginHealthStatus.Healthy, activated.Summary.HealthStatus);
        Assert.Single(runtime);
        Assert.Single(await repository.ListHealthChecksAsync());
    }

    [Fact]
    public async Task New_version_preserves_logical_history_and_is_installed()
    {
        var initial = Plugin(status: PluginStatus.Active, health: PluginHealthStatus.Healthy);
        var repository = new InMemoryPluginRepository([initial]);
        var service = new PluginStudioService(repository, new FakeProbe(PluginHealthStatus.Healthy));

        var created = await service.UpdateVersionAsync(initial.Id,
            new UpdatePluginVersionCommand("1.1.0", "builtin://test/plugin-v1-1", initial.Revision));
        var versions = await repository.GetVersionHistoryAsync(initial.PluginKey);

        Assert.Equal(PluginStatus.Installed, created.Summary.Status);
        Assert.Equal(PluginHealthStatus.Unknown, created.Summary.HealthStatus);
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.Equal(initial.PluginKey, version.PluginKey));
    }


    [Fact]
    public async Task Activating_successor_deactivates_previous_version_as_one_repository_update()
    {
        var logicalKey = Guid.NewGuid();
        var previous = Plugin(status: PluginStatus.Active, health: PluginHealthStatus.Healthy) with
        {
            PluginKey = logicalKey,
            Version = "1.0.0"
        };
        var successor = Plugin(status: PluginStatus.Installed, health: PluginHealthStatus.Healthy) with
        {
            PluginKey = logicalKey,
            Key = previous.Key,
            Version = "1.1.0",
            CreatedAt = previous.CreatedAt.AddMinutes(1),
            UpdatedAt = previous.UpdatedAt.AddMinutes(1)
        };
        var repository = new InMemoryPluginRepository([previous, successor]);
        var service = new PluginStudioService(repository, new FakeProbe(PluginHealthStatus.Healthy));

        await service.TransitionAsync(successor.Id, "activate");
        var versions = await repository.GetVersionHistoryAsync(logicalKey);

        Assert.Equal(PluginStatus.Active, versions.Single(item => item.Id == successor.Id).Status);
        Assert.Equal(PluginStatus.Inactive, versions.Single(item => item.Id == previous.Id).Status);
        Assert.Equal(1, repository.BatchUpdateCount);
    }

    [Fact]
    public async Task Incompatible_plugin_cannot_be_activated()
    {
        var incompatible = Plugin(platformApiVersion: "2.0", health: PluginHealthStatus.Healthy);
        var repository = new InMemoryPluginRepository([incompatible]);
        var service = new PluginStudioService(repository, new FakeProbe(PluginHealthStatus.Healthy));

        await Assert.ThrowsAsync<ConvoLab.Application.Common.Errors.DomainRuleViolationException>(
            () => service.TransitionAsync(incompatible.Id, "activate"));
    }

    private static PluginState Plugin(
        PluginStatus status = PluginStatus.Installed,
        PluginHealthStatus health = PluginHealthStatus.Unknown,
        string platformApiVersion = "1.0")
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        return new PluginState(
            id, id, "test-plugin", "Test plugin", "Test", "ConvoLab", "1.0.0",
            PluginCategory.Tool, status, health, health == PluginHealthStatus.Healthy ? "Ready" : "Not checked",
            "builtin://test/plugin", "TestPlugin", platformApiVersion, ["tool"], [], "{}",
            new Dictionary<string, string>(), health == PluginHealthStatus.Unknown ? null : now,
            1, now, now);
    }

    private sealed class FakeProbe(PluginHealthStatus status) : IPluginHealthProbe
    {
        public Task<PluginProbeResult> ProbeAsync(PluginProbeRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginProbeResult(status, status == PluginHealthStatus.Healthy ? "Ready" : "Failed", 4, "Test"));
    }

    private sealed class InMemoryPluginRepository(IReadOnlyList<PluginState> initial) : IPluginStudioRepository
    {
        private readonly List<PluginState> _plugins = initial.ToList();
        private readonly List<PluginHealthCheckState> _checks = [];
        public int BatchUpdateCount { get; private set; }

        public Task<int> CountPluginsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_plugins.Count);
        public Task<IReadOnlyList<PluginState>> ListPluginsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PluginState>>(_plugins.ToList());
        public Task<PluginState?> GetPluginAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_plugins.SingleOrDefault(item => item.Id == id));
        public Task<PluginState?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_plugins.Where(item => item.Key == key).OrderByDescending(item => item.UpdatedAt).FirstOrDefault());
        public Task<IReadOnlyList<PluginState>> GetVersionHistoryAsync(Guid pluginKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PluginState>>(_plugins.Where(item => item.PluginKey == pluginKey).OrderByDescending(item => item.CreatedAt).ToList());
        public Task AddPluginAsync(PluginState plugin, CancellationToken cancellationToken = default) { _plugins.Add(plugin); return Task.CompletedTask; }
        public Task UpdatePluginAsync(PluginState plugin, long expectedRevision, CancellationToken cancellationToken = default)
            => UpdatePluginsAsync([new PluginUpdateState(plugin, expectedRevision)], cancellationToken);
        public Task UpdatePluginsAsync(IReadOnlyList<PluginUpdateState> updates, CancellationToken cancellationToken = default)
        {
            BatchUpdateCount++;
            foreach (var update in updates)
            {
                var index = _plugins.FindIndex(item => item.Id == update.Plugin.Id);
                Assert.True(index >= 0);
                Assert.Equal(update.ExpectedRevision, _plugins[index].Revision);
                _plugins[index] = update.Plugin;
            }
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<PluginHealthCheckState>> ListHealthChecksAsync(int limit = 100, Guid? pluginId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginHealthCheckState>>(_checks.Where(item => !pluginId.HasValue || item.PluginId == pluginId).Take(limit).ToList());
        public Task AddHealthCheckAsync(PluginHealthCheckState healthCheck, CancellationToken cancellationToken = default) { _checks.Add(healthCheck); return Task.CompletedTask; }
        public Task RecordHealthCheckAsync(PluginUpdateState update, PluginHealthCheckState healthCheck, CancellationToken cancellationToken = default)
        {
            var index = _plugins.FindIndex(item => item.Id == update.Plugin.Id);
            Assert.True(index >= 0);
            Assert.Equal(update.ExpectedRevision, _plugins[index].Revision);
            _plugins[index] = update.Plugin;
            _checks.Add(healthCheck);
            return Task.CompletedTask;
        }
    }
}

