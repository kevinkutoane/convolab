using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Settings;

public interface ISecretProvider
{
    string Scheme { get; }
    Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct);
}

public sealed class SecretProviderEvidenceRegistry : ISecretProviderEvidenceSource
{
    private readonly ConcurrentDictionary<string, SecretProviderEvidence> _evidence =
        new(StringComparer.OrdinalIgnoreCase);

    public void Configured(string provider) => _evidence.TryAdd(
        provider,
        new SecretProviderEvidence(provider, OperationalDependencyState.Configured, null, null));

    public void Record(string provider, SecretResolutionStatus status, string? errorCode)
    {
        var state = status switch
        {
            SecretResolutionStatus.Resolved => OperationalDependencyState.LiveValidated,
            SecretResolutionStatus.Unavailable or SecretResolutionStatus.TimedOut =>
                OperationalDependencyState.Unavailable,
            _ => OperationalDependencyState.Degraded
        };
        _evidence[provider] = new(provider, state, DateTimeOffset.UtcNow, errorCode);
    }

    public IReadOnlyList<SecretProviderEvidence> Snapshot() =>
        _evidence.Values.OrderBy(item => item.Provider).ToArray();
}

public sealed class CompositeSecretStore : ISecretStore
{
    private readonly IReadOnlyDictionary<string, ISecretProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly SecretProviderEvidenceRegistry _evidence;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _resolutionGates =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _cacheGenerations =
        new(StringComparer.Ordinal);

    public CompositeSecretStore(
        IEnumerable<ISecretProvider> providers,
        IMemoryCache cache,
        SecretProviderEvidenceRegistry evidence,
        IConfiguration configuration)
    {
        _providers = providers.ToDictionary(provider => provider.Scheme, StringComparer.OrdinalIgnoreCase);
        _cache = cache;
        _evidence = evidence;
        _ttl = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("SecretStores:CacheTtlSeconds", 300), 1, 3600));
        foreach (var provider in _providers.Values) _evidence.Configured(provider.Scheme);
    }

    public async Task<SecretResolutionResult> ResolveAsync(
        string reference,
        CancellationToken ct = default)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("secret.resolve");
        var canonical = reference?.Trim() ?? string.Empty;
        string scheme;
        string key;
        try
        {
            (scheme, key) = Domain.Settings.SecretReference.ParseReference(canonical);
        }
        catch (ArgumentException)
        {
            return SecretResolutionResult.Failed(
                "unknown", SecretResolutionStatus.Invalid, "secret.reference.invalid");
        }
        canonical = $"{scheme.ToLowerInvariant()}:{key}";

        if (!_providers.TryGetValue(scheme, out var provider))
            return SecretResolutionResult.Failed(
                scheme, SecretResolutionStatus.Invalid, "secret.provider.unsupported");

        if (_cache.TryGetValue<string>(canonical, out var cached)
            && !string.IsNullOrWhiteSpace(cached))
            return SecretResolutionResult.Resolved(scheme, cached);

        var gate = _resolutionGates.GetOrAdd(canonical, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<string>(canonical, out cached)
                && !string.IsNullOrWhiteSpace(cached))
                return SecretResolutionResult.Resolved(scheme, cached);

            var generation = _cacheGenerations.GetOrAdd(canonical, 0);
            var result = await provider.ResolveAsync(key, ct);
            _evidence.Record(scheme, result.Status, result.ErrorCode);
            activity?.SetTag("secret.provider", scheme);
            activity?.SetTag("secret.outcome", result.Status.ToString());
            if (!result.IsResolved)
            {
                System.Diagnostics.TagList tags = default;
                tags.Add("provider", scheme);
                tags.Add("outcome", result.Status.ToString());
                ConvoLabTelemetry.SecretResolutionFailures.Add(1, tags);
            }
            if (result.IsResolved
                && _cacheGenerations.GetOrAdd(canonical, 0) == generation)
                _cache.Set(canonical, result.RevealValue()!, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _ttl,
                    Size = 1
                });
            return result;
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
                _resolutionGates.TryRemove(
                    new KeyValuePair<string, SemaphoreSlim>(canonical, gate));
        }
    }

    public async Task<SecretValidationResult> ValidateAsync(
        string reference,
        CancellationToken ct = default)
    {
        var result = await ResolveAsync(reference, ct);
        return new(result.Provider, result.Status, result.ErrorCode);
    }

    public void Invalidate(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        try
        {
            var (scheme, key) = Domain.Settings.SecretReference.ParseReference(reference.Trim());
            var canonical = $"{scheme.ToLowerInvariant()}:{key}";
            _cacheGenerations.AddOrUpdate(canonical, 1, (_, generation) => generation + 1);
            _cache.Remove(canonical);
        }
        catch (ArgumentException) { }
    }
}

internal sealed partial class EnvironmentSecretProvider : ISecretProvider
{
    public string Scheme => "env";

    public Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!EnvironmentVariableName().IsMatch(key))
            return Task.FromResult(SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.env.name_invalid"));
        var value = Environment.GetEnvironmentVariable(key);
        return Task.FromResult(string.IsNullOrWhiteSpace(value)
            ? SecretResolutionResult.Failed(Scheme, SecretResolutionStatus.Missing, "secret.env.missing")
            : SecretResolutionResult.Resolved(Scheme, value));
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableName();
}

internal sealed class DockerSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string Scheme => "docker-secret";

    public async Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)
            || Path.IsPathRooted(key)
            || key.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || key is "." or "..")
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.docker.name_invalid");

        var root = Path.GetFullPath(configuration["SecretStores:DockerSecretsRoot"] ?? "/run/secrets");
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.docker.path_escape");

        try
        {
            var rootDirectory = new DirectoryInfo(root);
            if (!rootDirectory.Exists)
                return SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Missing, "secret.docker.root_missing");
            if (!string.IsNullOrWhiteSpace(rootDirectory.LinkTarget)
                || rootDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Invalid, "secret.docker.root_symlink_rejected");
            if (!OperatingSystem.IsWindows())
            {
                var rootMode = File.GetUnixFileMode(root);
                if (rootMode.HasFlag(UnixFileMode.GroupWrite)
                    || rootMode.HasFlag(UnixFileMode.OtherWrite))
                    return SecretResolutionResult.Failed(
                        Scheme, SecretResolutionStatus.Invalid, "secret.docker.root_permissions_unsafe");
            }

            var file = new FileInfo(path);
            if (!file.Exists)
                return SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Missing, "secret.docker.missing");
            if (!string.IsNullOrWhiteSpace(file.LinkTarget)
                || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Invalid, "secret.docker.symlink_rejected");
            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(path);
                if (mode.HasFlag(UnixFileMode.GroupWrite) || mode.HasFlag(UnixFileMode.OtherWrite))
                    return SecretResolutionResult.Failed(
                        Scheme, SecretResolutionStatus.Invalid, "secret.docker.permissions_unsafe");
            }

            var value = (await File.ReadAllTextAsync(path, ct)).TrimEnd('\r', '\n');
            return string.IsNullOrWhiteSpace(value)
                ? SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Missing, "secret.docker.empty")
                : SecretResolutionResult.Resolved(Scheme, value);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.TimedOut, "secret.docker.timeout");
        }
        catch (IOException)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Unavailable, "secret.docker.unavailable");
        }
        catch (UnauthorizedAccessException)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Unavailable, "secret.docker.access_denied");
        }
    }
}

internal sealed partial class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ConcurrentDictionary<string, SecretClient> _clients =
        new(StringComparer.OrdinalIgnoreCase);

    public AzureKeyVaultSecretProvider(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public string Scheme => "azure-key-vault";

    public async Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
    {
        if (!TryParse(key, out var vaultUri, out var name, out var version))
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.azure.reference_invalid");

        var allowlist = _configuration.GetSection("SecretStores:AzureKeyVault:AllowedVaultUris")
            .Get<string[]>() ?? [];
        if (!allowlist.Any(item => IsExactAllowedVault(item, vaultUri)))
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.azure.vault_not_allowed");

        try
        {
            var client = _clients.GetOrAdd(vaultUri.AbsoluteUri, _ => CreateClient(vaultUri));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(
                _configuration.GetValue("SecretStores:AzureKeyVault:TimeoutSeconds", 10), 1, 60)));
            var response = await client.GetSecretAsync(name, version, timeout.Token);
            return string.IsNullOrWhiteSpace(response.Value.Value)
                ? SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Missing, "secret.azure.empty")
                : SecretResolutionResult.Resolved(Scheme, response.Value.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Missing, "secret.azure.missing");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.TimedOut, "secret.azure.timeout");
        }
        catch (RequestFailedException)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Unavailable, "secret.azure.unavailable");
        }
        catch (CredentialUnavailableException)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Unavailable, "secret.azure.credential_unavailable");
        }
        catch (AuthenticationFailedException)
        {
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Unavailable, "secret.azure.authentication_failed");
        }
    }

    private SecretClient CreateClient(Uri vaultUri)
    {
        var credential = AzureKeyVaultCredentialFactory.Create(_configuration, _environment);
        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                MaxRetries = Math.Clamp(
                    _configuration.GetValue("SecretStores:AzureKeyVault:MaxRetries", 3), 0, 5),
                NetworkTimeout = TimeSpan.FromSeconds(Math.Clamp(
                    _configuration.GetValue("SecretStores:AzureKeyVault:TimeoutSeconds", 10), 1, 60))
            }
        };
        return new SecretClient(vaultUri, credential, clientOptions);
    }

    private static bool TryParse(
        string value,
        out Uri vaultUri,
        out string name,
        out string? version)
    {
        vaultUri = null!;
        name = string.Empty;
        version = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrWhiteSpace(uri.UserInfo)
            || !string.IsNullOrWhiteSpace(uri.Query)
            || !string.IsNullOrWhiteSpace(uri.Fragment)) return false;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is < 2 or > 3
            || !segments[0].Equals("secrets", StringComparison.OrdinalIgnoreCase)) return false;
        vaultUri = new Uri(uri.GetLeftPart(UriPartial.Authority));
        name = Uri.UnescapeDataString(segments[1]);
        version = segments.Length == 3 ? Uri.UnescapeDataString(segments[2]) : null;
        return SecretName().IsMatch(name)
               && (version is null || SecretVersion().IsMatch(version));
    }

    private static bool IsExactAllowedVault(string value, Uri vaultUri) =>
        Uri.TryCreate(value, UriKind.Absolute, out var allowed)
        && allowed.Scheme == Uri.UriSchemeHttps
        && allowed.AbsolutePath is "" or "/"
        && string.IsNullOrEmpty(allowed.Query)
        && string.IsNullOrEmpty(allowed.Fragment)
        && string.Equals(
            allowed.GetLeftPart(UriPartial.Authority).TrimEnd('/'),
            vaultUri.GetLeftPart(UriPartial.Authority).TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^[A-Za-z0-9-]{1,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretName();

    [GeneratedRegex("^[A-Za-z0-9]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretVersion();
}

public static class AzureKeyVaultCredentialFactory
{
    public static TokenCredential Create(IConfiguration configuration, IHostEnvironment environment)
    {
        var clientId = configuration["SecretStores:AzureKeyVault:ManagedIdentityClientId"];
        if (!environment.IsProduction() && !environment.IsEnvironment("UAT"))
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId,
                ExcludeInteractiveBrowserCredential = true
            });
        }

        var managedIdentity = string.IsNullOrWhiteSpace(clientId)
            ? new ManagedIdentityCredential()
            : new ManagedIdentityCredential(clientId);
        return new ChainedTokenCredential(
            new WorkloadIdentityCredential(),
            managedIdentity);
    }
}
