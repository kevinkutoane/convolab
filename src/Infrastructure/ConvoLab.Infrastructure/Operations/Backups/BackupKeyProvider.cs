using System;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;
using ConvoLab.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class BackupKeyProvider : IBackupKeyProvider
{
    private const string KeyReference = "env:BACKUP_ENCRYPTION_KEY";
    private readonly ISecretStore _secretStore;
    private readonly ILogger<BackupKeyProvider> _logger;

    public BackupKeyProvider(ISecretStore secretStore, ILogger<BackupKeyProvider> logger)
    {
        _secretStore = secretStore;
        _logger = logger;
    }

    public async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _secretStore.ResolveAsync(KeyReference, cancellationToken);

        if (!result.IsResolved || string.IsNullOrWhiteSpace(result.RevealValue()))
        {
            _logger.LogError("Backup encryption key '{Reference}' could not be resolved from ISecretStore.", KeyReference);
            throw new InvalidOperationException($"Backup encryption key '{KeyReference}' is required but not configured or resolution failed. Insecure key fallbacks are strictly prohibited.");
        }

        var secretValue = result.RevealValue()!.Trim();

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(secretValue);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Backup encryption key '{Reference}' is not valid Base64.", KeyReference);
            throw new InvalidOperationException($"Backup encryption key '{KeyReference}' must be a valid Base64 encoded string.", ex);
        }

        if (keyBytes.Length != 32)
        {
            _logger.LogError("Backup encryption key '{Reference}' has invalid length {Length} bytes.", KeyReference, keyBytes.Length);
            throw new InvalidOperationException($"Backup encryption key '{KeyReference}' must decode to exactly 32 bytes (256 bits). Received {keyBytes.Length} bytes.");
        }

        return keyBytes;
    }
}
