using System;
using System.IO;
using System.Linq;
using ConvoLab.Application.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Operations;

internal sealed class PostgresBackupEvidenceSource : IBackupEvidenceSource
{
    private readonly OperationalBackupOptions _options;
    private readonly ILogger<PostgresBackupEvidenceSource> _logger;

    public PostgresBackupEvidenceSource(IOptions<OperationalBackupOptions> options, ILogger<PostgresBackupEvidenceSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public BackupEvidence Snapshot()
    {
        var backupDirectory = _options.DirectoryPath;
        var rpoMinutes = _options.ExpectedRpoMinutes;

        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            return new BackupEvidence(
                State: OperationalDependencyState.NotConfigured,
                Message: "Backup directory is not configured. Backups are not being monitored.",
                LastBackupCompletedAt: null,
                LastBackupVerifiedAt: null,
                LastBackupSizeBytes: null,
                ConfiguredRpo: null);
        }

        if (!Directory.Exists(backupDirectory))
        {
            _logger.LogWarning("Configured backup directory does not exist: {Directory}", backupDirectory);
            return new BackupEvidence(
                State: OperationalDependencyState.Unavailable,
                Message: $"Configured backup directory '{backupDirectory}' does not exist or is inaccessible.",
                LastBackupCompletedAt: null,
                LastBackupVerifiedAt: null,
                LastBackupSizeBytes: null,
                ConfiguredRpo: rpoMinutes.HasValue ? TimeSpan.FromMinutes(rpoMinutes.Value) : null);
        }

        try
        {
            var latestBackup = new DirectoryInfo(backupDirectory)
                .GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Extension.Equals(".dump", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestBackup == null)
            {
                return new BackupEvidence(
                    State: OperationalDependencyState.Degraded,
                    Message: "Backup directory is configured and accessible, but no valid backup files were found.",
                    LastBackupCompletedAt: null,
                    LastBackupVerifiedAt: null,
                    LastBackupSizeBytes: null,
                    ConfiguredRpo: rpoMinutes.HasValue ? TimeSpan.FromMinutes(rpoMinutes.Value) : null);
            }

            var completedAt = new DateTimeOffset(latestBackup.LastWriteTimeUtc);
            var configuredRpo = rpoMinutes.HasValue ? TimeSpan.FromMinutes(rpoMinutes.Value) : (TimeSpan?)null;

            var state = OperationalDependencyState.Configured;
            var message = "A recent backup file was found in the configured directory.";

            if (configuredRpo.HasValue && (DateTimeOffset.UtcNow - completedAt) > configuredRpo.Value)
            {
                state = OperationalDependencyState.Degraded;
                message = $"The latest backup is older than the configured RPO of {configuredRpo.Value.TotalMinutes} minutes.";
            }

            return new BackupEvidence(
                State: state,
                Message: message,
                LastBackupCompletedAt: completedAt,
                LastBackupVerifiedAt: null, // Verification is a separate DR exercise process, null until automated test restores are implemented
                LastBackupSizeBytes: latestBackup.Length,
                ConfiguredRpo: configuredRpo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read backup directory: {Directory}", backupDirectory);
            return new BackupEvidence(
                State: OperationalDependencyState.Unavailable,
                Message: "Failed to read the backup directory due to an exception.",
                LastBackupCompletedAt: null,
                LastBackupVerifiedAt: null,
                LastBackupSizeBytes: null,
                ConfiguredRpo: rpoMinutes.HasValue ? TimeSpan.FromMinutes(rpoMinutes.Value) : null);
        }
    }
}
