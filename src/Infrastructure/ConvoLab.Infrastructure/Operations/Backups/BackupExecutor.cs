using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class BackupExecutor : IBackupExecutor
{
    private readonly PostgresBackupTooling _postgresTooling;
    private readonly DocumentStorageArchiver _documentArchiver;
    private readonly DataProtectionArchiver _dataProtectionArchiver;
    private readonly IBackupEncryptor _encryptor;
    private readonly IBackupStore _backupStore;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BackupExecutor> _logger;

    public BackupExecutor(
        PostgresBackupTooling postgresTooling,
        DocumentStorageArchiver documentArchiver,
        DataProtectionArchiver dataProtectionArchiver,
        IBackupEncryptor encryptor,
        IBackupStore backupStore,
        IHostEnvironment hostEnvironment,
        ILogger<BackupExecutor> logger)
    {
        _postgresTooling = postgresTooling;
        _documentArchiver = documentArchiver;
        _dataProtectionArchiver = dataProtectionArchiver;
        _encryptor = encryptor;
        _backupStore = backupStore;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<BackupArtifact> ExecuteBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupId = $"convolab-backup-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";
        _logger.LogInformation("Beginning orchestrated backup {BackupId}", backupId);

        var tempDir = Path.Combine(Path.GetTempPath(), "convolab-backup-tmp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Database dump
            var rawDbPath = Path.Combine(tempDir, "database.raw.dump");
            var dbDumpSuccess = await _postgresTooling.ExecuteDumpAsync(rawDbPath, cancellationToken);
            if (!dbDumpSuccess)
            {
                throw new InvalidOperationException("PostgreSQL database dump failed.");
            }

            var dbBytes = await File.ReadAllBytesAsync(rawDbPath, cancellationToken);
            var dbSha256 = Convert.ToHexString(SHA256.HashData(dbBytes)).ToLowerInvariant();
            var dbArtifact = new BackupDatabaseArtifact(
                Format: "postgres-custom",
                SchemaVersion: "202608200001",
                File: "database.dump",
                SizeBytes: dbBytes.Length,
                Sha256: dbSha256);

            // 2. Documents archive
            using var rawDocsStream = new MemoryStream();
            var docsArchived = await _documentArchiver.ArchiveDocumentsAsync(rawDocsStream, cancellationToken);
            var docsBytes = rawDocsStream.ToArray();
            var docsSha256 = docsBytes.Length > 0 ? Convert.ToHexString(SHA256.HashData(docsBytes)).ToLowerInvariant() : null;
            var docsArtifact = new BackupStorageArtifact(
                Included: docsArchived && docsBytes.Length > 0,
                Archive: docsBytes.Length > 0 ? "documents.tar.zst" : null,
                SizeBytes: docsBytes.Length > 0 ? docsBytes.Length : null,
                Sha256: docsSha256);

            // 3. Data protection keys archive
            using var rawDpStream = new MemoryStream();
            var dpArchived = await _dataProtectionArchiver.ArchiveKeyRingAsync(rawDpStream, cancellationToken);
            var dpBytes = rawDpStream.ToArray();
            var dpSha256 = dpBytes.Length > 0 ? Convert.ToHexString(SHA256.HashData(dpBytes)).ToLowerInvariant() : null;
            var dpArtifact = new BackupStorageArtifact(
                Included: dpArchived && dpBytes.Length > 0,
                Archive: dpBytes.Length > 0 ? "dataprotection.tar.zst" : null,
                SizeBytes: dpBytes.Length > 0 ? dpBytes.Length : null,
                Sha256: dpSha256);

            // 4. Construct manifest
            var manifest = new BackupArtifact(
                BackupId: backupId,
                CreatedAt: DateTimeOffset.UtcNow,
                PlatformVersion: "1.0.0-alpha.16",
                SourceCommit: "git-head",
                Environment: _hostEnvironment.EnvironmentName,
                Database: dbArtifact,
                Documents: docsArtifact,
                DataProtection: dpArtifact,
                Configuration: new BackupConfigurationArtifact(Included: true, SecretValuesExcluded: true),
                Tooling: new BackupToolingArtifact(PostgresVersion: "16", BackupToolVersion: "1.0"));

            // 5. Encrypt streams and save to store
            using var encryptedDbStream = new MemoryStream();
            using var unencryptedDbStream = new MemoryStream(dbBytes);
            await _encryptor.EncryptAsync(manifest, unencryptedDbStream, encryptedDbStream, cancellationToken);

            MemoryStream? encryptedDocsStream = null;
            if (docsBytes.Length > 0)
            {
                encryptedDocsStream = new MemoryStream();
                using var unencryptedDocsStream = new MemoryStream(docsBytes);
                await _encryptor.EncryptAsync(manifest, unencryptedDocsStream, encryptedDocsStream, cancellationToken);
            }

            MemoryStream? encryptedDpStream = null;
            if (dpBytes.Length > 0)
            {
                encryptedDpStream = new MemoryStream();
                using var unencryptedDpStream = new MemoryStream(dpBytes);
                await _encryptor.EncryptAsync(manifest, unencryptedDpStream, encryptedDpStream, cancellationToken);
            }

            await _backupStore.SaveAsync(
                backupId,
                manifest,
                encryptedDbStream,
                encryptedDocsStream,
                encryptedDpStream,
                cancellationToken);

            encryptedDocsStream?.Dispose();
            encryptedDpStream?.Dispose();

            _logger.LogInformation("Backup {BackupId} successfully generated and persisted.", backupId);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* Ignore temp cleanup */ }
            }
        }
    }
}
