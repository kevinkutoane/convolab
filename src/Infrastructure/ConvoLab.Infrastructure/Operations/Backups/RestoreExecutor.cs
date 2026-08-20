using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class RestoreExecutor : IRestoreExecutor
{
    private readonly ConcurrentDictionary<Guid, RestoreOperationStatus> _operations = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RestoreExecutor> _logger;

    public RestoreExecutor(IServiceScopeFactory scopeFactory, ILogger<RestoreExecutor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<Guid> EnqueueRestoreAsync(RestoreOptions options, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid();
        var status = new RestoreOperationStatus(
            OperationId: operationId,
            BackupId: options.BackupId,
            State: RestoreOperationState.Queued,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: null);

        _operations[operationId] = status;

        _ = Task.Run(() => ExecuteRestoreProcessAsync(operationId, options));

        return Task.FromResult(operationId);
    }

    public Task<RestoreOperationStatus?> GetRestoreStatusAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        _operations.TryGetValue(operationId, out var status);
        return Task.FromResult(status);
    }

    private async Task ExecuteRestoreProcessAsync(Guid operationId, RestoreOptions options)
    {
        _logger.LogInformation("Beginning asynchronous restore operation {OperationId} for backup {BackupId}", operationId, options.BackupId);

        using var scope = _scopeFactory.CreateScope();
        var backupStore = scope.ServiceProvider.GetRequiredService<IBackupStore>();
        var encryptor = scope.ServiceProvider.GetRequiredService<IBackupEncryptor>();
        var postgresTooling = scope.ServiceProvider.GetRequiredService<PostgresBackupTooling>();
        var documentArchiver = scope.ServiceProvider.GetRequiredService<DocumentStorageArchiver>();
        var dataProtectionArchiver = scope.ServiceProvider.GetRequiredService<DataProtectionArchiver>();
        var recoveryVerifier = scope.ServiceProvider.GetRequiredService<IRecoveryVerifier>();

        var tempDir = Path.Combine(Path.GetTempPath(), "convolab-restore-tmp-" + operationId.ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Validating manifest
            UpdateState(operationId, RestoreOperationState.Validating);
            var manifest = await backupStore.GetManifestAsync(options.BackupId);
            if (manifest == null)
            {
                Fail(operationId, $"Backup manifest for {options.BackupId} could not be found.");
                return;
            }

            // 2. Decrypting database
            UpdateState(operationId, RestoreOperationState.Decrypting);
            await using var encryptedDbStream = await backupStore.OpenDatabaseStreamAsync(options.BackupId);
            if (encryptedDbStream == null)
            {
                Fail(operationId, "Database stream could not be loaded from backup store.");
                return;
            }

            var decryptedDbPath = Path.Combine(tempDir, "database.decrypted.dump");
            await using (var decryptedDbFile = File.Create(decryptedDbPath))
            {
                var decResult = await encryptor.DecryptAsync(manifest, encryptedDbStream, decryptedDbFile);
                if (!decResult.Success)
                {
                    Fail(operationId, $"Database decryption failed: {decResult.ErrorMessage}");
                    return;
                }
            }

            // 3. Restoring database
            UpdateState(operationId, RestoreOperationState.RestoringDatabase);
            var dbRestoreOk = await postgresTooling.ExecuteRestoreAsync(decryptedDbPath, cleanTarget: options.AllowDestructive);
            if (!dbRestoreOk)
            {
                Fail(operationId, "PostgreSQL restore process failed.");
                return;
            }

            // 4. Restoring documents if included
            if (manifest.Documents.Included)
            {
                UpdateState(operationId, RestoreOperationState.RestoringDocuments);
                await using var encryptedDocsStream = await backupStore.OpenDocumentsStreamAsync(options.BackupId);
                if (encryptedDocsStream != null)
                {
                    using var decryptedDocsStream = new MemoryStream();
                    var docDecResult = await encryptor.DecryptAsync(manifest, encryptedDocsStream, decryptedDocsStream);
                    if (docDecResult.Success)
                    {
                        decryptedDocsStream.Position = 0;
                        await documentArchiver.RestoreDocumentsAsync(decryptedDocsStream);
                    }
                }
            }

            // 5. Restoring Data Protection keys if policy is PreserveWhenVerified
            if (options.SessionRecoveryMode == SessionRecoveryMode.PreserveWhenVerified && manifest.DataProtection.Included)
            {
                UpdateState(operationId, RestoreOperationState.RestoringDataProtection);
                await using var encryptedDpStream = await backupStore.OpenDataProtectionStreamAsync(options.BackupId);
                if (encryptedDpStream != null)
                {
                    using var decryptedDpStream = new MemoryStream();
                    var dpDecResult = await encryptor.DecryptAsync(manifest, encryptedDpStream, decryptedDpStream);
                    if (dpDecResult.Success)
                    {
                        decryptedDpStream.Position = 0;
                        await dataProtectionArchiver.RestoreKeyRingAsync(decryptedDpStream);
                    }
                }
            }

            // 6. Verifying restored state
            UpdateState(operationId, RestoreOperationState.Verifying);
            var verifyResult = await recoveryVerifier.VerifyRecoveryAsync();
            if (!verifyResult.IsHealthy)
            {
                Fail(operationId, $"Post-restore recovery verification failed: {string.Join("; ", verifyResult.Inconsistencies)}");
                return;
            }

            // 7. Completed
            if (_operations.TryGetValue(operationId, out var current))
            {
                _operations[operationId] = current with
                {
                    State = RestoreOperationState.Completed,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }

            _logger.LogInformation("Asynchronous restore operation {OperationId} completed successfully.", operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered during asynchronous restore operation {OperationId}.", operationId);
            Fail(operationId, $"Unhandled restore exception: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* Ignore temp cleanup */ }
            }
        }
    }

    private void UpdateState(Guid operationId, RestoreOperationState state)
    {
        if (_operations.TryGetValue(operationId, out var current))
        {
            _operations[operationId] = current with { State = state };
        }
    }

    private void Fail(Guid operationId, string error)
    {
        _logger.LogError("Restore operation {OperationId} failed: {Error}", operationId, error);
        if (_operations.TryGetValue(operationId, out var current))
        {
            _operations[operationId] = current with
            {
                State = RestoreOperationState.Failed,
                ErrorMessage = error,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
