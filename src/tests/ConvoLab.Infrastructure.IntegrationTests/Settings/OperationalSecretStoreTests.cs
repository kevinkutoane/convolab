using System.Text.Json;
using Azure.Identity;
using Azure.Core;
using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

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
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretStores:CacheTtlSeconds"] = "300"
            }).Build());

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
            new ConfigurationBuilder().Build());

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => store.ResolveAsync("env:ALPHA")));

        Assert.All(results, result => Assert.True(result.IsResolved));
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("Production", typeof(ChainedTokenCredential))]
    [InlineData("UAT", typeof(ChainedTokenCredential))]
    [InlineData("Development", typeof(DefaultAzureCredential))]
    public void Azure_credentials_are_restricted_outside_development(string environment, Type expectedType)
    {
        var credential = AzureKeyVaultCredentialFactory.Create(
            new ConfigurationBuilder().Build(), new TestEnvironment(environment));
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
            var provider = new DockerSecretProvider(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SecretStores:DockerSecretsRoot"] = root
                }).Build());

            var result = await provider.ResolveAsync(name, CancellationToken.None);

            Assert.Equal(SecretResolutionStatus.Invalid, result.Status);
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
            var provider = new DockerSecretProvider(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SecretStores:DockerSecretsRoot"] = root
                }).Build());
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

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ConvoLab.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
