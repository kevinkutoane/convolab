using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConvoLab.Application.Operations.Backups;

public sealed record BackupArtifact(
    string BackupId,
    DateTimeOffset CreatedAt,
    string PlatformVersion,
    string SourceCommit,
    string Environment,
    BackupDatabaseArtifact Database,
    BackupStorageArtifact Documents,
    BackupStorageArtifact DataProtection,
    BackupConfigurationArtifact Configuration,
    BackupToolingArtifact Tooling);

public sealed record BackupDatabaseArtifact(
    string Format,
    string SchemaVersion,
    string File,
    long SizeBytes,
    string Sha256);

public sealed record BackupStorageArtifact(
    bool Included,
    string? Archive,
    long? SizeBytes,
    string? Sha256);

public sealed record BackupConfigurationArtifact(
    bool Included,
    bool SecretValuesExcluded);

public sealed record BackupToolingArtifact(
    string PostgresVersion,
    string BackupToolVersion);

public sealed record EncryptionResult(
    string Algorithm,
    string KeyProvider,
    string KeyVersionId);

public sealed record DecryptionResult(bool Success, string? ErrorMessage);

public interface IBackupExecutor
{
    Task<BackupArtifact> ExecuteBackupAsync(CancellationToken cancellationToken = default);
}

public interface IBackupStore
{
    Task SaveAsync(string backupId, BackupArtifact manifest, Stream databaseStream, Stream? documentsStream, Stream? dataProtectionStream, CancellationToken cancellationToken = default);
    Task<BackupArtifact?> GetManifestAsync(string backupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupArtifact>> ListBackupsAsync(CancellationToken cancellationToken = default);
    Task<Stream?> OpenDatabaseStreamAsync(string backupId, CancellationToken cancellationToken = default);
    Task<Stream?> OpenDocumentsStreamAsync(string backupId, CancellationToken cancellationToken = default);
    Task<Stream?> OpenDataProtectionStreamAsync(string backupId, CancellationToken cancellationToken = default);
}

public interface IBackupEncryptor
{
    Task<EncryptionResult> EncryptAsync(BackupArtifact artifact, Stream source, Stream destination, CancellationToken cancellationToken = default);
    Task<DecryptionResult> DecryptAsync(BackupArtifact artifact, Stream source, Stream destination, CancellationToken cancellationToken = default);
}

public interface IBackupKeyProvider
{
    Task<byte[]> GetKeyAsync(CancellationToken cancellationToken = default);
}

public interface IBackupVerifier
{
    Task<bool> VerifyChecksumsAsync(string backupId, CancellationToken cancellationToken = default);
}

public interface IBackupRetentionService
{
    Task EnforceRetentionPolicyAsync(CancellationToken cancellationToken = default);
}

public enum SessionRecoveryMode
{
    Invalidate,
    PreserveWhenVerified
}

public sealed record RestoreOptions(
    string BackupId,
    bool AllowDestructive,
    SessionRecoveryMode SessionRecoveryMode);

public enum RestoreOperationState
{
    Queued,
    Validating,
    Decrypting,
    RestoringDatabase,
    RestoringDocuments,
    RestoringDataProtection,
    Verifying,
    Completed,
    Failed
}

public sealed record RestoreOperationStatus(
    Guid OperationId,
    string BackupId,
    RestoreOperationState State,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public interface IRestoreExecutor
{
    Task<Guid> EnqueueRestoreAsync(RestoreOptions options, CancellationToken cancellationToken = default);
    Task<RestoreOperationStatus?> GetRestoreStatusAsync(Guid operationId, CancellationToken cancellationToken = default);
}
