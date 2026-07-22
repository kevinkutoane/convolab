using ConvoLab.Domain.Plugins.Aggregates;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Domain.Plugins.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Plugins;

public sealed class PluginTests
{
    [Fact]
    public void Registered_plugin_without_health_evidence_cannot_be_activated()
    {
        var plugin = Create();

        Assert.Throws<InvalidOperationException>(() => plugin.Activate());
        Assert.Equal(PluginStatus.Installed, plugin.Status);
    }

    [Fact]
    public void Registered_plugin_requires_health_evidence_before_activation_when_unhealthy()
    {
        var plugin = Create();
        plugin.RecordHealth(PluginHealthStatus.Unhealthy, "Probe failed", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => plugin.Activate());
        Assert.Equal(PluginStatus.Installed, plugin.Status);
    }

    [Fact]
    public void Active_plugin_is_immutable_until_deactivated()
    {
        var plugin = Create();
        plugin.RecordHealth(PluginHealthStatus.Healthy, "Ready", DateTimeOffset.UtcNow);
        plugin.Activate();

        Assert.Throws<InvalidOperationException>(() => plugin.UpdateMetadata(
            "Changed", "Description", "Publisher", PluginCategory.Tool, "Entry", "1.0", ["tool"], []));

        plugin.Deactivate();
        plugin.UpdateMetadata("Changed", "Description", "Publisher", PluginCategory.Tool, "Entry", "1.0", ["tool"], []);
        Assert.Equal("Changed", plugin.Name);
        Assert.Equal(PluginStatus.Inactive, plugin.Status);
    }

    [Fact]
    public void Version_change_resets_health_and_activation_state()
    {
        var plugin = Create();
        plugin.RecordHealth(PluginHealthStatus.Healthy, "Ready", DateTimeOffset.UtcNow);
        plugin.Activate();
        plugin.Deactivate();

        plugin.UpdateVersion(PluginVersion.FromString("2.0.0"), "builtin://test/v2");

        Assert.Equal("2.0.0", plugin.Version.Value);
        Assert.Equal(PluginStatus.Installed, plugin.Status);
        Assert.Equal(PluginHealthStatus.Unknown, plugin.HealthStatus);
        Assert.Null(plugin.LastHealthCheckAt);
    }

    private static Plugin Create() => Plugin.Register(
        "test-plugin", "Test plugin", "Test", "ConvoLab", PluginCategory.Tool,
        PluginVersion.FromString("1.0.0"), "builtin://test/plugin", "TestPlugin", "1.0",
        ["tool"], []);
}
