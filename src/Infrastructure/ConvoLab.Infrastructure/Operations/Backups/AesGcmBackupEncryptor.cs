using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class AesGcmBackupEncryptor : IBackupEncryptor
{
    private static readonly byte[] Magic = "CVLB_GCM_V1"u8.ToArray(); // 11 bytes
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int ChunkSize = 64 * 1024; // 64 KB chunks

    private readonly IBackupKeyProvider _keyProvider;

    public AesGcmBackupEncryptor(IBackupKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public async Task<EncryptionResult> EncryptAsync(BackupArtifact artifact, Stream source, Stream destination, CancellationToken cancellationToken = default)
    {
        var key = await _keyProvider.GetKeyAsync(cancellationToken);
        if (key.Length != 32)
        {
            throw new InvalidOperationException($"Encryption key must be exactly 32 bytes for AES-256-GCM. Received {key.Length} bytes.");
        }

        // Header: [Magic: 11B][BaseNonce: 12B][ChunkSize: 4B]
        var baseNonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(baseNonce);

        await destination.WriteAsync(Magic, cancellationToken);
        await destination.WriteAsync(baseNonce, cancellationToken);

        var chunkSizeBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(chunkSizeBytes, ChunkSize);
        await destination.WriteAsync(chunkSizeBytes, cancellationToken);

        using var aesGcm = new AesGcm(key, TagSize);

        var buffer = new byte[ChunkSize];
        var ciphertextBuffer = new byte[ChunkSize];
        var tag = new byte[TagSize];
        var currentNonce = new byte[NonceSize];
        ulong chunkIndex = 0;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Compute unique deterministic nonce for chunk: baseNonce XOR chunkIndex
            Array.Copy(baseNonce, currentNonce, NonceSize);
            BinaryPrimitives.WriteUInt64BigEndian(currentNonce.AsSpan(4, 8), BinaryPrimitives.ReadUInt64BigEndian(currentNonce.AsSpan(4, 8)) ^ chunkIndex);

            // Associated authenticated data: [ChunkIndex: 8B][PayloadLength: 4B]
            var aad = new byte[12];
            BinaryPrimitives.WriteUInt64BigEndian(aad.AsSpan(0, 8), chunkIndex);
            BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(8, 4), bytesRead);

            EncryptChunk(aesGcm, currentNonce, buffer, ciphertextBuffer, tag, aad, bytesRead);

            // Chunk frame: [PayloadLength: 4B][Tag: 16B][Ciphertext: PayloadLength B]
            var frameLen = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(frameLen, bytesRead);

            await destination.WriteAsync(frameLen, cancellationToken);
            await destination.WriteAsync(tag, cancellationToken);
            await destination.WriteAsync(ciphertextBuffer.AsMemory(0, bytesRead), cancellationToken);

            chunkIndex++;
        }

        // End-of-stream frame: PayloadLength = 0
        var eofFrame = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(eofFrame, 0);
        await destination.WriteAsync(eofFrame, cancellationToken);

        return new EncryptionResult("AES-256-GCM-Chunked-V1", "ISecretStore", "v1");
    }

    private static void EncryptChunk(
        AesGcm aesGcm,
        byte[] nonce,
        byte[] buffer,
        byte[] ciphertextBuffer,
        byte[] tag,
        byte[] aad,
        int bytesRead)
    {
        aesGcm.Encrypt(
            nonce.AsSpan(),
            buffer.AsSpan(0, bytesRead),
            ciphertextBuffer.AsSpan(0, bytesRead),
            tag.AsSpan(),
            aad.AsSpan());
    }

    public async Task<DecryptionResult> DecryptAsync(BackupArtifact artifact, Stream source, Stream destination, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = await _keyProvider.GetKeyAsync(cancellationToken);
            if (key.Length != 32)
            {
                return new DecryptionResult(false, $"Decryption failed: Key must be 32 bytes for AES-256-GCM. Received {key.Length} bytes.");
            }

            // Read and verify Magic
            var magicBuffer = new byte[Magic.Length];
            var magicRead = await ReadExactAsync(source, magicBuffer, cancellationToken);
            if (magicRead < Magic.Length || !magicBuffer.AsSpan().SequenceEqual(Magic))
            {
                return new DecryptionResult(false, "Decryption failed: Invalid archive magic header or unsupported encryption envelope.");
            }

            // Read BaseNonce
            var baseNonce = new byte[NonceSize];
            if (await ReadExactAsync(source, baseNonce, cancellationToken) < NonceSize)
            {
                return new DecryptionResult(false, "Decryption failed: Incomplete nonce header.");
            }

            // Read ChunkSize
            var chunkSizeBytes = new byte[4];
            if (await ReadExactAsync(source, chunkSizeBytes, cancellationToken) < 4)
            {
                return new DecryptionResult(false, "Decryption failed: Incomplete chunk size header.");
            }
            var configuredChunkSize = BinaryPrimitives.ReadInt32BigEndian(chunkSizeBytes);
            if (configuredChunkSize <= 0 || configuredChunkSize > 10 * 1024 * 1024)
            {
                return new DecryptionResult(false, $"Decryption failed: Invalid chunk size {configuredChunkSize}.");
            }

            using var aesGcm = new AesGcm(key, TagSize);

            var ciphertextBuffer = new byte[configuredChunkSize];
            var plaintextBuffer = new byte[configuredChunkSize];
            var tag = new byte[TagSize];
            var currentNonce = new byte[NonceSize];
            var lengthBuffer = new byte[4];
            ulong chunkIndex = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await ReadExactAsync(source, lengthBuffer, cancellationToken) < 4)
                {
                    return new DecryptionResult(false, "Decryption failed: Unexpected end of stream while reading chunk length.");
                }

                var chunkLen = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                if (chunkLen == 0)
                {
                    // End of stream marker reached cleanly
                    break;
                }

                if (chunkLen < 0 || chunkLen > configuredChunkSize)
                {
                    return new DecryptionResult(false, $"Decryption failed: Invalid chunk length {chunkLen}.");
                }

                // Read Tag
                if (await ReadExactAsync(source, tag, cancellationToken) < TagSize)
                {
                    return new DecryptionResult(false, "Decryption failed: Incomplete authentication tag.");
                }

                // Read Ciphertext
                if (await ReadExactAsync(source, ciphertextBuffer.AsMemory(0, chunkLen), cancellationToken) < chunkLen)
                {
                    return new DecryptionResult(false, "Decryption failed: Incomplete ciphertext chunk.");
                }

                // Derive Nonce
                Array.Copy(baseNonce, currentNonce, NonceSize);
                BinaryPrimitives.WriteUInt64BigEndian(currentNonce.AsSpan(4, 8), BinaryPrimitives.ReadUInt64BigEndian(currentNonce.AsSpan(4, 8)) ^ chunkIndex);

                // Reconstruct AAD
                var aad = new byte[12];
                BinaryPrimitives.WriteUInt64BigEndian(aad.AsSpan(0, 8), chunkIndex);
                BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(8, 4), chunkLen);

                try
                {
                    DecryptChunk(aesGcm, currentNonce, ciphertextBuffer, tag, plaintextBuffer, aad, chunkLen);
                }
                catch (AuthenticationTagMismatchException)
                {
                    return new DecryptionResult(false, $"Cryptographic verification failed: Authentication tag mismatch at chunk index {chunkIndex}. Archive has been tampered with or corrupted.");
                }

                await destination.WriteAsync(plaintextBuffer.AsMemory(0, chunkLen), cancellationToken);
                chunkIndex++;
            }

            return new DecryptionResult(true, null);
        }
        catch (Exception ex)
        {
            return new DecryptionResult(false, $"Decryption failed with exception: {ex.Message}");
        }
    }

    private static void DecryptChunk(
        AesGcm aesGcm,
        byte[] nonce,
        byte[] ciphertextBuffer,
        byte[] tag,
        byte[] plaintextBuffer,
        byte[] aad,
        int chunkLen)
    {
        aesGcm.Decrypt(
            nonce.AsSpan(),
            ciphertextBuffer.AsSpan(0, chunkLen),
            tag.AsSpan(),
            plaintextBuffer.AsSpan(0, chunkLen),
            aad.AsSpan());
    }

    private static async Task<int> ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], ct);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
