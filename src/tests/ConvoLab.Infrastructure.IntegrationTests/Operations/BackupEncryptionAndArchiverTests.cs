using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;
using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Operations.Backups;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

public sealed class BackupEncryptionAndArchiverTests
{
    private sealed class MockSecretStore : ISecretStore
    {
        private readonly string? _key;
        public MockSecretStore(string? key) => _key = key;

        public Task<SecretResolutionResult> ResolveAsync(string reference, CancellationToken ct = default)
        {
            if (_key == null)
            {
                return Task.FromResult(SecretResolutionResult.Failed("env", SecretResolutionStatus.Missing, "secret_missing"));
            }
            return Task.FromResult(SecretResolutionResult.Resolved("env", _key));
        }

        public Task<SecretValidationResult> ValidateAsync(string reference, CancellationToken ct = default) =>
            Task.FromResult(new SecretValidationResult("env", SecretResolutionStatus.Resolved));

        public void Invalidate(string reference) { }
    }

    private sealed class StaticKeyProvider : IBackupKeyProvider
    {
        private readonly byte[] _key;
        public StaticKeyProvider(byte[] key) => _key = key;
        public Task<byte[]> GetKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
    }

    [Fact]
    public async Task KeyProvider_throws_when_secret_missing()
    {
        var store = new MockSecretStore(null);
        var provider = new BackupKeyProvider(store, NullLogger<BackupKeyProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetKeyAsync());
    }

    [Fact]
    public async Task KeyProvider_throws_when_key_length_invalid()
    {
        // 16 bytes encoded in Base64 (invalid for AES-256)
        var invalidKey = Convert.ToBase64String(new byte[16]);
        var store = new MockSecretStore(invalidKey);
        var provider = new BackupKeyProvider(store, NullLogger<BackupKeyProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetKeyAsync());
    }

    [Fact]
    public async Task KeyProvider_returns_exact_32_bytes_when_valid()
    {
        var validBytes = new byte[32];
        Random.Shared.NextBytes(validBytes);
        var validBase64 = Convert.ToBase64String(validBytes);

        var store = new MockSecretStore(validBase64);
        var provider = new BackupKeyProvider(store, NullLogger<BackupKeyProvider>.Instance);

        var result = await provider.GetKeyAsync();
        Assert.Equal(validBytes, result);
    }

    [Fact]
    public async Task Encryptor_successfully_roundtrips_data_with_chunked_AesGcm()
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var keyProvider = new StaticKeyProvider(key);
        var encryptor = new AesGcmBackupEncryptor(keyProvider);

        var originalPayload = new byte[200 * 1024]; // 200 KB across multiple 64KB chunks
        Random.Shared.NextBytes(originalPayload);

        using var source = new MemoryStream(originalPayload);
        using var encryptedDestination = new MemoryStream();

        var dummyManifest = new BackupArtifact(
            "test-id", DateTimeOffset.UtcNow, "1.0.0", "commit", "dev",
            new BackupDatabaseArtifact("postgres", "v1", "db.dump", 0, "hash"),
            new BackupStorageArtifact(false, null, null, null),
            new BackupStorageArtifact(false, null, null, null),
            new BackupConfigurationArtifact(true, true),
            new BackupToolingArtifact("16", "1.0"));

        var encResult = await encryptor.EncryptAsync(dummyManifest, source, encryptedDestination);
        Assert.Equal("AES-256-GCM-Chunked-V1", encResult.Algorithm);

        encryptedDestination.Position = 0;
        using var decryptedDestination = new MemoryStream();

        var decResult = await encryptor.DecryptAsync(dummyManifest, encryptedDestination, decryptedDestination);
        Assert.True(decResult.Success);
        Assert.Null(decResult.ErrorMessage);

        Assert.Equal(originalPayload, decryptedDestination.ToArray());
    }

    [Fact]
    public async Task Encryptor_detects_tampered_ciphertext_chunks()
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var keyProvider = new StaticKeyProvider(key);
        var encryptor = new AesGcmBackupEncryptor(keyProvider);

        var originalPayload = Encoding.UTF8.GetBytes("Critical platform state and audit records that must not be tampered with.");
        using var source = new MemoryStream(originalPayload);
        using var encryptedDestination = new MemoryStream();

        var dummyManifest = new BackupArtifact(
            "test-id", DateTimeOffset.UtcNow, "1.0.0", "commit", "dev",
            new BackupDatabaseArtifact("postgres", "v1", "db.dump", 0, "hash"),
            new BackupStorageArtifact(false, null, null, null),
            new BackupStorageArtifact(false, null, null, null),
            new BackupConfigurationArtifact(true, true),
            new BackupToolingArtifact("16", "1.0"));

        await encryptor.EncryptAsync(dummyManifest, source, encryptedDestination);

        var encryptedBytes = encryptedDestination.ToArray();
        // Tamper with a byte in the middle of ciphertext
        encryptedBytes[^10] ^= 0xFF;

        using var tamperedStream = new MemoryStream(encryptedBytes);
        using var decryptedDestination = new MemoryStream();

        var decResult = await encryptor.DecryptAsync(dummyManifest, tamperedStream, decryptedDestination);
        Assert.False(decResult.Success);
        Assert.Contains("Authentication tag mismatch", decResult.ErrorMessage);
    }
}
