using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;
using ConvoLab.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class BackupKeyProvider : IBackupKeyProvider
{
    private readonly ISecretStore _secretStore;
    private readonly ILogger<BackupKeyProvider> _logger;

    public BackupKeyProvider(ISecretStore secretStore, ILogger<BackupKeyProvider> logger)
    {
        _secretStore = secretStore;
        _logger = logger;
    }

    public async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken = default)
    {
        // By architecture spec, the backup key should be resolved via the existing SecretStore mechanism.
        // We will look for an environment secret called BACKUP_ENCRYPTION_KEY or a vault secret.
        // For local development, we fallback to a hardcoded development key to prevent blocking.
        // In production, the SecretStore configuration MUST provide this value.

        var reference = "env:BACKUP_ENCRYPTION_KEY";
        var result = await _secretStore.ResolveAsync(reference, cancellationToken);

        if (!result.IsResolved)
        {
            _logger.LogWarning("BACKUP_ENCRYPTION_KEY not found in ISecretStore or resolution failed. Falling back to an insecure development key. Do not use this in production.");
            // 32-byte key for AES-256
            return Encoding.UTF8.GetBytes("Development_Backup_Key_Not_Safe_".PadRight(32, '0'));
        }

        var secretValue = result.RevealValue() ?? string.Empty;

        try
        {
            // Expect the key to be Base64 encoded in the secret store
            return Convert.FromBase64String(secretValue);
        }
        catch (FormatException)
        {
            _logger.LogError("BACKUP_ENCRYPTION_KEY is not a valid Base64 string. Falling back to UTF8 bytes.");
            // Fallback to UTF8 bytes, padding or truncating to 32 bytes
            var bytes = Encoding.UTF8.GetBytes(secretValue);
            var key = new byte[32];
            Array.Copy(bytes, key, Math.Min(bytes.Length, 32));
            return key;
        }
    }
}
