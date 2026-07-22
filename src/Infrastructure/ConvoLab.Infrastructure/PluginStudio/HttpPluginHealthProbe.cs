using System.Diagnostics;
using System.Net;
using ConvoLab.Application.PluginStudio;
using ConvoLab.Domain.Plugins.Enums;

namespace ConvoLab.Infrastructure.PluginStudio;

public sealed class HttpPluginHealthProbe(IHttpClientFactory httpClientFactory) : IPluginHealthProbe
{
    private static readonly IReadOnlyDictionary<string, string> BuiltInManifests =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["deterministic-provider"] = "builtin://intelligence/deterministic",
            ["local-knowledge-connector"] = "builtin://knowledge/local-files",
            ["evaluation-metrics-pack"] = "builtin://evaluation/default",
            ["persistent-trace-exporter"] = "builtin://tracing/persistent"
        };

    public async Task<PluginProbeResult> ProbeAsync(PluginProbeRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (request.ManifestUrl.StartsWith("builtin://", StringComparison.OrdinalIgnoreCase))
        {
            stopwatch.Stop();
            if (!BuiltInManifests.TryGetValue(request.Key, out var expectedManifest)
                || !expectedManifest.Equals(request.ManifestUrl, StringComparison.OrdinalIgnoreCase))
            {
                return new PluginProbeResult(
                    PluginHealthStatus.Unhealthy,
                    "The built-in manifest is not registered by this ConvoLab runtime.",
                    (int)stopwatch.ElapsedMilliseconds,
                    "BuiltInRegistry");
            }
            return new PluginProbeResult(
                PluginHealthStatus.Healthy,
                "Built-in adapter is available in the current API process.",
                (int)stopwatch.ElapsedMilliseconds,
                "BuiltIn");
        }

        if (!Uri.TryCreate(request.ManifestUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            stopwatch.Stop();
            return new PluginProbeResult(
                PluginHealthStatus.Unhealthy,
                "The manifest URI is not a supported built-in, HTTP, or HTTPS endpoint.",
                (int)stopwatch.ElapsedMilliseconds,
                "Manifest");
        }

        if (!string.IsNullOrWhiteSpace(uri.UserInfo) || IsUnsafeHostName(uri.Host))
        {
            stopwatch.Stop();
            return new PluginProbeResult(
                PluginHealthStatus.Unhealthy,
                "The manifest endpoint is blocked by the Plugin Center network-safety policy.",
                (int)stopwatch.ElapsedMilliseconds,
                "NetworkPolicy");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, timeout.Token);
            if (addresses.Length == 0 || addresses.Any(IsUnsafeAddress))
            {
                stopwatch.Stop();
                return new PluginProbeResult(
                    PluginHealthStatus.Unhealthy,
                    "The manifest endpoint resolves to a private, local, or otherwise blocked network address.",
                    (int)stopwatch.ElapsedMilliseconds,
                    "NetworkPolicy");
            }
            var client = httpClientFactory.CreateClient("PluginHealth");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
            requestMessage.Headers.UserAgent.ParseAdd("ConvoLab-Plugin-Health/1.0");
            using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            stopwatch.Stop();
            if (response.IsSuccessStatusCode)
                return new PluginProbeResult(PluginHealthStatus.Healthy,
                    $"Manifest endpoint responded with {(int)response.StatusCode}.",
                    (int)stopwatch.ElapsedMilliseconds, "Http");
            return new PluginProbeResult(PluginHealthStatus.Unhealthy,
                $"Manifest endpoint responded with {(int)response.StatusCode} {response.ReasonPhrase}.",
                (int)stopwatch.ElapsedMilliseconds, "Http");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new PluginProbeResult(PluginHealthStatus.Unhealthy,
                "Manifest health check timed out after five seconds.",
                (int)stopwatch.ElapsedMilliseconds, "Http");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new PluginProbeResult(PluginHealthStatus.Unhealthy,
                $"Manifest health check failed: {exception.Message}",
                (int)stopwatch.ElapsedMilliseconds, "Http");
        }
    }
    private static bool IsUnsafeHostName(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var address) && IsUnsafeAddress(address);
    }

    private static bool IsUnsafeAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsUnsafeAddress(address.MapToIPv4());

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal)
            return true;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var ipv6 = address.GetAddressBytes();
            return (ipv6[0] & 0xFE) == 0xFC;
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return true;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 0
               || bytes[0] == 10
               || bytes[0] == 127
               || (bytes[0] == 169 && bytes[1] == 254)
               || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
               || (bytes[0] == 192 && bytes[1] == 168);
    }
}
