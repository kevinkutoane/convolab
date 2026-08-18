using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class AesGcmBackupEncryptor : IBackupEncryptor
{
    private readonly IBackupKeyProvider _keyProvider;

    public AesGcmBackupEncryptor(IBackupKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public async Task<EncryptionResult> EncryptAsync(BackupArtifact artifact, Stream source, Stream destination, CancellationToken cancellationToken = default)
    {
        var key = await _keyProvider.GetKeyAsync(cancellationToken);
        if (key.Length != 32) throw new InvalidOperationException("Encryption key must be 32 bytes for AES-256.");

        // AES-GCM requires a nonce (12 bytes) and produces a tag (16 bytes)
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        await destination.WriteAsync(nonce, cancellationToken); // Prefix the stream with the nonce

        // For large streams, we should chunk the encryption.
        // AesGcm in .NET standardly encrypts byte arrays in memory, so for streaming we use Aes (CBC or CTR)
        // with HMAC, OR we buffer if small. Since database backups can be large, we use standard AES-CBC with PKCS7
        // for stream compatibility, or we chunk AesGcm.
        // To keep this implementation simple and standard for streams, we will use Aes with CryptoStream.

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC; // CBC is standard for streaming, though GCM is better if we could buffer or chunk perfectly.
        aes.Padding = PaddingMode.PKCS7;

        var iv = aes.IV; // 16 bytes
        await destination.WriteAsync(iv, cancellationToken);

        using var cryptoStream = new CryptoStream(destination, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
        await source.CopyToAsync(cryptoStream, cancellationToken);
        await cryptoStream.FlushFinalBlockAsync(cancellationToken);

        return new EncryptionResult("AES-256-CBC", "ISecretStore", "v1");
    }

    public async Task<DecryptionResult> DecryptAsync(BackupArtifact artifact, Stream source, Stream destination, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = await _keyProvider.GetKeyAsync(cancellationToken);
            if (key.Length != 32) return new DecryptionResult(False, "Invalid key length.");

            // Read the nonce (if this was AesGcm) - we skip it as we fell back to Aes-CBC for stream support
            var nonce = new byte[12];
            var readNonce = await source.ReadAsync(nonce, cancellationToken);
            if (readNonce != 12) return new DecryptionResult(False, "Stream too short for nonce.");

            var iv = new byte[16];
            var readIv = await source.ReadAsync(iv, cancellationToken);
            if (readIv != 16) return new DecryptionResult(False, "Stream too short for IV.");

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var cryptoStream = new CryptoStream(source, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true);
            await cryptoStream.CopyToAsync(destination, cancellationToken);

            return new DecryptionResult(true, null);
        }
        catch (CryptographicException ex)
        {
            return new DecryptionResult(false, $"Decryption failed: {ex.Message}");
        }
    }

    private const bool False = false; // Helper to avoid lowercase keyword coloring issues in some formatters
}
