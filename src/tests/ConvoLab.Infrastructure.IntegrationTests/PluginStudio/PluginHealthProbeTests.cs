using ConvoLab.Application.PluginStudio;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Infrastructure.PluginStudio;

namespace ConvoLab.Infrastructure.IntegrationTests.PluginStudio;

public sealed class PluginHealthProbeTests
{
    [Fact]
    public async Task Known_built_in_manifest_is_healthy_without_an_external_request()
    {
        var probe = new HttpPluginHealthProbe(new RejectingHttpClientFactory());

        var result = await probe.ProbeAsync(new PluginProbeRequest(
            "deterministic-provider",
            "builtin://intelligence/deterministic",
            "DeterministicIntelligenceExecutor",
            PluginCategory.Provider));

        Assert.Equal(PluginHealthStatus.Healthy, result.Status);
        Assert.Equal("BuiltIn", result.Source);
    }

    [Fact]
    public async Task Unregistered_built_in_manifest_is_not_trusted()
    {
        var probe = new HttpPluginHealthProbe(new RejectingHttpClientFactory());

        var result = await probe.ProbeAsync(new PluginProbeRequest(
            "untrusted-plugin",
            "builtin://untrusted/adapter",
            "UntrustedAdapter",
            PluginCategory.Tool));

        Assert.Equal(PluginHealthStatus.Unhealthy, result.Status);
        Assert.Equal("BuiltInRegistry", result.Source);
    }

    [Fact]
    public async Task Local_manifest_endpoints_are_blocked_before_an_external_request()
    {
        var probe = new HttpPluginHealthProbe(new RejectingHttpClientFactory());

        var result = await probe.ProbeAsync(new PluginProbeRequest(
            "local-plugin",
            "http://127.0.0.1/plugin.json",
            "LocalAdapter",
            PluginCategory.Tool));

        Assert.Equal(PluginHealthStatus.Unhealthy, result.Status);
        Assert.Equal("NetworkPolicy", result.Source);
    }

    private sealed class RejectingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => throw new InvalidOperationException("No HTTP request should be created by this test.");
    }
}
