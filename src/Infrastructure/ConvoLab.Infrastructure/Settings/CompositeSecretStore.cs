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
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Settings;

public interface ISecretProvider
{
    string Scheme { get; }
    OperationalDependencyState InitialState => OperationalDependencyState.Configured;
    OperationalDependencyState SuccessfulValidationState => OperationalDependencyState.LiveValidated;
    Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct);
}

public sealed class SecretProviderEvidenceRegistry : ISecretProviderEvidenceSource
{
    private readonly ConcurrentDictionary<string, SecretProviderEvidence> _evidence =
        new(StringComparer.OrdinalIgnoreCase);

    public void Initialize(string provider, OperationalDependencyState state) => _evidence.TryAdd(
        provider,
        new SecretProviderEvidence(provider, state, null, null));

    public void Record(
        string provider,
        SecretResolutionStatus status,
        string? errorCode,
        OperationalDependencyState successfulValidationState)
    {
        var state = status switch
        {
            SecretResolutionStatus.Resolved => successfulValidationState,
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
        IOptions<SecretStoreOptions> options)
    {
        _providers = providers.ToDictionary(provider => provider.Scheme, StringComparer.OrdinalIgnoreCase);
        _cache = cache;
        _evidence = evidence;
        _ttl = TimeSpan.FromSeconds(Math.Clamp(
            options.Value.CacheTtlSeconds, 1, 3600));
        foreach (var provider in _providers.Values)
            _evidence.Initialize(provider.Scheme, provider.InitialState);
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
            _evidence.Record(
                scheme,
                result.Status,
                result.ErrorCode,
                provider.SuccessfulValidationState);
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
        var state = result.Status switch
        {
            SecretResolutionStatus.Resolved => providerState(reference),
            SecretResolutionStatus.Unavailable or SecretResolutionStatus.TimedOut
                => OperationalDependencyState.Unavailable,
            _ => OperationalDependencyState.Degraded
        };
        return new(result.Provider, result.Status, result.ErrorCode, state);

        OperationalDependencyState providerState(string value)
        {
            try
            {
                var (scheme, _) = Domain.Settings.SecretReference.ParseReference(value.Trim());
                return _providers.TryGetValue(scheme, out var configured)
                    ? configured.SuccessfulValidationState
                    : OperationalDependencyState.Degraded;
            }
            catch (ArgumentException)
            {
                return OperationalDependencyState.Degraded;
            }
        }
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

internal sealed class DockerSecretProvider(IOptions<SecretStoreOptions> options) : ISecretProvider
{
    public string Scheme => "docker-secret";
    public OperationalDependencyState InitialState =>
        string.IsNullOrWhiteSpace(options.Value.DockerSecretsRoot)
            ? OperationalDependencyState.NotConfigured
            : OperationalDependencyState.Configured;

    public async Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)
            || Path.IsPathRooted(key)
            || key.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || key is "." or "..")
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.docker.name_invalid");

        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.DockerSecretsRoot)
            ? "/run/secrets"
            : options.Value.DockerSecretsRoot);
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

internal interface IAzureKeyVaultSecretClient
{
    Task<string?> GetSecretValueAsync(
        string name,
        string? version,
        CancellationToken ct);
}

internal interface IAzureKeyVaultSecretClientFactory
{
    OperationalDependencyState SuccessfulValidationState { get; }
    IAzureKeyVaultSecretClient Create(Uri vaultUri);
}

internal sealed class AzureKeyVaultSecretClient(SecretClient client) : IAzureKeyVaultSecretClient
{
    public async Task<string?> GetSecretValueAsync(
        string name,
        string? version,
        CancellationToken ct) =>
        (await client.GetSecretAsync(name, version, ct)).Value.Value;
}

internal sealed class AzureKeyVaultSecretClientFactory(
    IOptions<SecretStoreOptions> options,
    IHostEnvironment environment) : IAzureKeyVaultSecretClientFactory
{
    public OperationalDependencyState SuccessfulValidationState =>
        OperationalDependencyState.LiveValidated;

    public IAzureKeyVaultSecretClient Create(Uri vaultUri)
    {
        var configured = options.Value.AzureKeyVault;
        var credential = AzureKeyVaultCredentialFactory.Create(configured, environment);
        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                // Retries are controlled by the provider so their limit is deterministic.
                MaxRetries = 0,
                NetworkTimeout = TimeSpan.FromSeconds(
                    Math.Clamp(configured.TimeoutSeconds, 1, 60))
            }
        };
        return new AzureKeyVaultSecretClient(new SecretClient(vaultUri, credential, clientOptions));
    }
}

internal sealed partial class AzureKeyVaultSecretProvider(
    IOptions<SecretStoreOptions> options,
    IAzureKeyVaultSecretClientFactory clientFactory) : ISecretProvider
{
    private readonly ConcurrentDictionary<string, IAzureKeyVaultSecretClient> _clients =
        new(StringComparer.OrdinalIgnoreCase);

    public string Scheme => "azure-key-vault";
    public OperationalDependencyState SuccessfulValidationState =>
        clientFactory.SuccessfulValidationState;
    public OperationalDependencyState InitialState =>
        options.Value.AzureKeyVault.AllowedVaultUris.Length == 0
            ? OperationalDependencyState.NotConfigured
            : OperationalDependencyState.Configured;

    public async Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
    {
        if (!TryParse(key, out var vaultUri, out var name, out var version))
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.azure.reference_invalid");

        var configured = options.Value.AzureKeyVault;
        var allowlist = configured.AllowedVaultUris;
        if (!allowlist.Any(item => IsExactAllowedVault(item, vaultUri)))
            return SecretResolutionResult.Failed(
                Scheme, SecretResolutionStatus.Invalid, "secret.azure.vault_not_allowed");

        try
        {
            var client = _clients.GetOrAdd(vaultUri.AbsoluteUri, _ => clientFactory.Create(vaultUri));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(
                configured.TimeoutSeconds, 1, 60)));
            string? value = null;
            for (var attempt = 0; attempt <= configured.MaxRetries; attempt++)
            {
                try
                {
                    value = await client.GetSecretValueAsync(name, version, timeout.Token);
                    break;
                }
                catch (RequestFailedException exception) when (
                    attempt < configured.MaxRetries && IsRetryable(exception.Status))
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(Math.Min(100 * (1 << attempt), 1000)),
                        timeout.Token);
                }
            }
            return string.IsNullOrWhiteSpace(value)
                ? SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Missing, "secret.azure.empty")
                : SecretResolutionResult.Resolved(Scheme, value);
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

    private static bool IsRetryable(int status) => status is 408 or 429 or >= 500;

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
    public static TokenCredential Create(
        AzureKeyVaultOptions options,
        IHostEnvironment environment)
    {
        var clientId = options.ManagedIdentityClientId;
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
