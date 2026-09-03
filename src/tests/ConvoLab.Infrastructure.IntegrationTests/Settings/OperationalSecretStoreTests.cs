using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Core;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Settings;

public sealed class OperationalSecretStoreTests
{
    [Fact]
    public void Resolution_result_never_serializes_or_stringifies_the_value()
    {
        const string sentinel = "sentinel-secret-value";
        var result = SecretResolutionResult.Resolved("env", sentinel);

        Assert.DoesNotContain(sentinel, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Successful_values_are_cached_by_canonical_reference_and_invalidated()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var store = new CompositeSecretStore(
            [provider], cache, new SecretProviderEvidenceRegistry(),
            Options.Create(new SecretStoreOptions { CacheTtlSeconds = 300 }));

        Assert.True((await store.ResolveAsync("ENV:ALPHA")).IsResolved);
        Assert.True((await store.ResolveAsync("env:ALPHA")).IsResolved);
        Assert.Equal(1, provider.Calls);

        store.Invalidate(" env:ALPHA ");
        Assert.True((await store.ResolveAsync("env:ALPHA")).IsResolved);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task Concurrent_resolution_is_single_flight()
    {
        var provider = new CountingProvider(TimeSpan.FromMilliseconds(50));
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 20 });
        var store = new CompositeSecretStore(
            [provider], cache, new SecretProviderEvidenceRegistry(),
            Options.Create(new SecretStoreOptions()));

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => store.ResolveAsync("env:ALPHA")));

        Assert.All(results, result => Assert.True(result.IsResolved));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Successful_cache_entries_expire_and_failed_resolutions_are_never_cached()
    {
        var provider = new SequenceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var store = new CompositeSecretStore(
            [provider], cache, new SecretProviderEvidenceRegistry(),
            Options.Create(new SecretStoreOptions { CacheTtlSeconds = 1 }));

        var failed = await store.ResolveAsync("env:ALPHA");
        var resolved = await store.ResolveAsync("env:ALPHA");
        var cached = await store.ResolveAsync("env:ALPHA");

        Assert.Equal(SecretResolutionStatus.Unavailable, failed.Status);
        Assert.True(resolved.IsResolved);
        Assert.True(cached.IsResolved);
        Assert.Equal(2, provider.Calls);

        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        Assert.True((await store.ResolveAsync("env:ALPHA")).IsResolved);
        Assert.Equal(3, provider.Calls);
    }

    [Theory]
    [InlineData("Production", typeof(ChainedTokenCredential))]
    [InlineData("UAT", typeof(ChainedTokenCredential))]
    [InlineData("Development", typeof(DefaultAzureCredential))]
    public void Azure_credentials_are_restricted_outside_development(string environment, Type expectedType)
    {
        var credential = AzureKeyVaultCredentialFactory.Create(
            new AzureKeyVaultOptions(), new TestEnvironment(environment));
        Assert.IsType(expectedType, credential);
        if (credential is ChainedTokenCredential chain)
        {
            var sourcesField = typeof(ChainedTokenCredential)
                .GetFields(System.Reflection.BindingFlags.Instance
                           | System.Reflection.BindingFlags.NonPublic)
                .Single(field => field.FieldType.IsArray
                                 && typeof(TokenCredential).IsAssignableFrom(
                                     field.FieldType.GetElementType()));
            var sources = Assert.IsAssignableFrom<IEnumerable<TokenCredential>>(
                sourcesField.GetValue(chain));
            Assert.Collection(
                sources,
                source => Assert.IsType<WorkloadIdentityCredential>(source),
                source => Assert.IsType<ManagedIdentityCredential>(source));
        }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("folder/secret")]
    [InlineData("folder\\secret")]
    [InlineData("..")]
    public async Task Docker_secret_names_reject_traversal_and_separators(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), $"convolab-docker-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var provider = DockerProvider(root);

            var result = await provider.ResolveAsync(name, CancellationToken.None);

            Assert.Equal(SecretResolutionStatus.Invalid, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Docker_secret_valid_missing_and_absolute_paths_are_handled_without_leakage()
    {
        const string sentinel = "docker-secret-sentinel-value";
        var root = Path.Combine(Path.GetTempPath(), $"convolab-docker-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "valid-secret"), sentinel + Environment.NewLine);
        try
        {
            var provider = DockerProvider(root);
            var valid = await provider.ResolveAsync("valid-secret", CancellationToken.None);
            var missing = await provider.ResolveAsync("missing-secret", CancellationToken.None);
            var absolute = await provider.ResolveAsync(
                Path.Combine(root, "valid-secret"), CancellationToken.None);

            Assert.True(valid.IsResolved);
            Assert.Equal(sentinel, valid.RevealValue());
            Assert.Equal(SecretResolutionStatus.Missing, missing.Status);
            Assert.Equal(SecretResolutionStatus.Invalid, absolute.Status);
            Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(valid), StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, valid.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Docker_secret_symlink_or_reparse_point_is_rejected_when_supported()
    {
        var root = Path.Combine(Path.GetTempPath(), $"convolab-docker-secrets-{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"convolab-docker-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var target = Path.Combine(outside, "outside-secret");
        await File.WriteAllTextAsync(target, "must-not-be-read");
        var link = Path.Combine(root, "linked-secret");
        try
        {
            try
            {
                File.CreateSymbolicLink(link, target);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                    or IOException
                    or PlatformNotSupportedException)
            {
                return;
            }

            var result = await DockerProvider(root)
                .ResolveAsync("linked-secret", CancellationToken.None);

            Assert.Equal(SecretResolutionStatus.Invalid, result.Status);
            Assert.Equal("secret.docker.symlink_rejected", result.ErrorCode);
        }
        finally
        {
            if (File.Exists(link)) File.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Docker_secret_unsafe_writable_permissions_are_rejected_on_unix()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), $"convolab-docker-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "unsafe-secret");
        await File.WriteAllTextAsync(path, "must-not-be-read");
        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite);
            var result = await DockerProvider(root)
                .ResolveAsync("unsafe-secret", CancellationToken.None);

            Assert.Equal(SecretResolutionStatus.Invalid, result.Status);
            Assert.Equal("secret.docker.permissions_unsafe", result.ErrorCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Docker_secret_resolution_honors_caller_cancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"convolab-docker-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "cancelled"), "not-returned");
        try
        {
            var provider = DockerProvider(root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                provider.ResolveAsync("cancelled", cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Azure_stub_allowlist_and_validation_state_are_truthful_and_sanitized()
    {
        const string sentinel = "azure-vault-sentinel-value";
        var options = Options.Create(new SecretStoreOptions
        {
            AzureKeyVault = new AzureKeyVaultOptions
            {
                AllowedVaultUris = ["https://allowed.vault.azure.net"]
            }
        });
        var factory = new StubAzureFactory(_ => Task.FromResult<string?>(sentinel));
        var provider = new AzureKeyVaultSecretProvider(options, factory);
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var store = new CompositeSecretStore(
            [provider], cache, new SecretProviderEvidenceRegistry(), options);

        var validation = await store.ValidateAsync(
            "azure-key-vault:https://allowed.vault.azure.net/secrets/example/version1");
        var forbidden = await store.ResolveAsync(
            "azure-key-vault:https://other.vault.azure.net/secrets/example");
        var invalid = await store.ResolveAsync("azure-key-vault:not-a-reference");

        Assert.True(validation.IsValid);
        Assert.Equal(OperationalDependencyState.StubValidated, validation.DependencyState);
        Assert.Equal(SecretResolutionStatus.Invalid, forbidden.Status);
        Assert.Equal("secret.azure.vault_not_allowed", forbidden.ErrorCode);
        Assert.Equal(SecretResolutionStatus.Invalid, invalid.Status);
        Assert.Equal(1, factory.Calls);
        var serialized = JsonSerializer.Serialize(new { validation, forbidden, invalid });
        Assert.DoesNotContain(sentinel, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("allowed.vault", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Azure_stub_timeout_cancellation_and_retry_limit_are_bounded()
    {
        var timeoutOptions = Options.Create(new SecretStoreOptions
        {
            AzureKeyVault = new AzureKeyVaultOptions
            {
                AllowedVaultUris = ["https://allowed.vault.azure.net"],
                TimeoutSeconds = 1,
                MaxRetries = 0
            }
        });
        var timeoutFactory = new StubAzureFactory(async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return null;
        });
        var timedOut = await new AzureKeyVaultSecretProvider(timeoutOptions, timeoutFactory)
            .ResolveAsync("https://allowed.vault.azure.net/secrets/example", CancellationToken.None);
        Assert.Equal(SecretResolutionStatus.TimedOut, timedOut.Status);
        Assert.Equal("secret.azure.timeout", timedOut.ErrorCode);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AzureKeyVaultSecretProvider(timeoutOptions, timeoutFactory)
                .ResolveAsync("https://allowed.vault.azure.net/secrets/example", cancelled.Token));

        const string responseBodySentinel = "provider-response-body-must-not-leak";
        var retryOptions = Options.Create(new SecretStoreOptions
        {
            AzureKeyVault = new AzureKeyVaultOptions
            {
                AllowedVaultUris = ["https://allowed.vault.azure.net"],
                TimeoutSeconds = 5,
                MaxRetries = 2
            }
        });
        var retryFactory = new StubAzureFactory(_ =>
            Task.FromException<string?>(new RequestFailedException(503, responseBodySentinel)));
        var unavailable = await new AzureKeyVaultSecretProvider(retryOptions, retryFactory)
            .ResolveAsync("https://allowed.vault.azure.net/secrets/example", CancellationToken.None);

        Assert.Equal(3, retryFactory.Calls);
        Assert.Equal(SecretResolutionStatus.Unavailable, unavailable.Status);
        Assert.Equal("secret.azure.unavailable", unavailable.ErrorCode);
        Assert.DoesNotContain(responseBodySentinel, JsonSerializer.Serialize(unavailable), StringComparison.Ordinal);
        Assert.DoesNotContain(responseBodySentinel, unavailable.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Secret_reference_update_and_disable_invalidate_old_new_and_disabled_references()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var organisationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Organisations.Add(new OrganisationRecord
        {
            Id = organisationId,
            Name = "Secret invalidation organisation",
            Slug = $"secret-invalidation-{organisationId:N}",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Workspaces.Add(new WorkspaceRecord
        {
            Id = workspaceId,
            OrganisationId = organisationId,
            Name = "Secret invalidation workspace",
            Slug = $"secret-invalidation-{workspaceId:N}",
            Description = "Secret invalidation acceptance",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SecretReferences.Add(new SecretReferenceRecord
        {
            Id = referenceId,
            WorkspaceId = workspaceId,
            DisplayName = "Original reference",
            Reference = "env:ORIGINAL_REFERENCE",
            Provider = "env",
            Status = "NotValidated",
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            Revision = 1
        });
        await db.SaveChangesAsync();
        var store = new CapturingInvalidationStore();
        var service = new SecretReferenceService(db, store);

        var updated = await service.UpdateAsync(
            workspaceId,
            referenceId,
            new UpdateSecretReferenceRequest(
                "Updated reference", "docker-secret:updated-reference", 1),
            actorId,
            "Secret tester",
            "secret-invalidation-correlation");
        await service.DisableAsync(
            workspaceId,
            referenceId,
            updated.Revision,
            actorId,
            "Secret tester",
            "secret-invalidation-correlation");

        Assert.Equal(
            [
                "env:ORIGINAL_REFERENCE",
                "docker-secret:updated-reference",
                "docker-secret:updated-reference"
            ],
            store.Invalidated);
    }

    private sealed class CountingProvider(TimeSpan? delay = null) : ISecretProvider
    {
        private int _calls;
        public int Calls => _calls;
        public string Scheme => "env";
        public async Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            if (delay is not null) await Task.Delay(delay.Value, ct);
            return SecretResolutionResult.Resolved(Scheme, "resolved-value");
        }
    }

    private sealed class SequenceProvider : ISecretProvider
    {
        public int Calls { get; private set; }
        public string Scheme => "env";
        public Task<SecretResolutionResult> ResolveAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(Calls == 1
                ? SecretResolutionResult.Failed(
                    Scheme, SecretResolutionStatus.Unavailable, "secret.test.unavailable")
                : SecretResolutionResult.Resolved(Scheme, "resolved-value"));
        }
    }

    private sealed class StubAzureFactory(
        Func<CancellationToken, Task<string?>> resolve) : IAzureKeyVaultSecretClientFactory
    {
        private int _calls;
        public int Calls => _calls;
        public OperationalDependencyState SuccessfulValidationState =>
            OperationalDependencyState.StubValidated;
        public IAzureKeyVaultSecretClient Create(Uri vaultUri) => new Client(this, resolve);

        private sealed class Client(
            StubAzureFactory owner,
            Func<CancellationToken, Task<string?>> resolve) : IAzureKeyVaultSecretClient
        {
            public Task<string?> GetSecretValueAsync(
                string name,
                string? version,
                CancellationToken ct)
            {
                Interlocked.Increment(ref owner._calls);
                return resolve(ct);
            }
        }
    }

    private sealed class CapturingInvalidationStore : ISecretStore
    {
        public List<string> Invalidated { get; } = [];
        public Task<SecretResolutionResult> ResolveAsync(
            string reference,
            CancellationToken ct = default) =>
            Task.FromResult(SecretResolutionResult.Resolved("env", "not-exposed"));
        public Task<SecretValidationResult> ValidateAsync(
            string reference,
            CancellationToken ct = default) =>
            Task.FromResult(new SecretValidationResult(
                "env",
                SecretResolutionStatus.Resolved,
                null,
                OperationalDependencyState.StubValidated));
        public void Invalidate(string reference) => Invalidated.Add(reference);
        public void Clear() { }
    }

    private static DockerSecretProvider DockerProvider(string root) => new(
        Options.Create(new SecretStoreOptions { DockerSecretsRoot = root }));

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ConvoLab.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
