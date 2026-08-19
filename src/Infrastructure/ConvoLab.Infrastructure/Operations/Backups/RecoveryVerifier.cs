using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Backups;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class RecoveryVerifier : IRecoveryVerifier
{
    private readonly ApplicationDbContext _db;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly string _documentStoragePath;
    private readonly string _keyRingPath;
    private readonly ILogger<RecoveryVerifier> _logger;

    public RecoveryVerifier(
        ApplicationDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<RecoveryVerifier> logger)
    {
        _db = db;
        _dataProtectionProvider = dataProtectionProvider;
        _documentStoragePath = configuration["Knowledge:StoragePath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "knowledge-documents");
        _keyRingPath = configuration["DataProtection:KeyRingPath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "keys");
        _logger = logger;
    }

    public async Task<RecoveryVerificationResult> VerifyRecoveryAsync(CancellationToken cancellationToken = default)
    {
        var inconsistencies = new List<string>();

        // 1. Database Connectivity & Entity Counts
        var canConnect = false;
        var migrationsUpToDate = false;
        long userCount = 0;
        long workspaceCount = 0;
        long externalIdentityCount = 0;
        long policyCount = 0;
        long evaluationCount = 0;
        long traceCount = 0;
        long analyticsEventCount = 0;

        try
        {
            canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                inconsistencies.Add("Database connection could not be established.");
            }
            else
            {
                var pendingMigrations = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
                migrationsUpToDate = !pendingMigrations.Any();
                if (!migrationsUpToDate)
                {
                    inconsistencies.Add($"Database has {pendingMigrations.Count()} pending migrations.");
                }

                userCount = await _db.IdentityUsers.LongCountAsync(cancellationToken);
                workspaceCount = await _db.Workspaces.LongCountAsync(cancellationToken);
                externalIdentityCount = await _db.ExternalIdentities.LongCountAsync(cancellationToken);
                policyCount = await _db.PolicyDefinitions.LongCountAsync(cancellationToken);
                evaluationCount = await _db.EvaluationScorecards.LongCountAsync(cancellationToken);
                traceCount = await _db.Traces.LongCountAsync(cancellationToken);
                analyticsEventCount = await _db.AnalyticsEvents.LongCountAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed database verification probe.");
            inconsistencies.Add($"Database query exception: {ex.Message}");
        }

        // 2. Document Storage Reconciliation
        long dbDocumentCount = 0;
        long physicalFileCount = 0;
        long missingFiles = 0;
        long orphanFiles = 0;
        var documentsReconciled = false;

        try
        {
            if (canConnect)
            {
                var storageKeys = await _db.KnowledgeDocuments
                    .AsNoTracking()
                    .Select(d => d.StorageKey)
                    .ToListAsync(cancellationToken);

                dbDocumentCount = storageKeys.Count;

                var physicalFiles = Directory.Exists(_documentStoragePath)
                    ? new DirectoryInfo(_documentStoragePath)
                        .GetFiles("*.*", SearchOption.AllDirectories)
                        .Where(f => !f.Name.StartsWith(".probe-", StringComparison.OrdinalIgnoreCase))
                        .Select(f => Path.GetRelativePath(_documentStoragePath, f.FullName).Replace('\\', '/'))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : [];

                physicalFileCount = physicalFiles.Count;

                foreach (var key in storageKeys)
                {
                    if (!physicalFiles.Contains(key))
                    {
                        missingFiles++;
                    }
                }

                foreach (var file in physicalFiles)
                {
                    if (!storageKeys.Contains(file))
                    {
                        orphanFiles++;
                    }
                }

                if (missingFiles > 0)
                {
                    inconsistencies.Add($"Document storage reconciliation mismatch: {missingFiles} documents referenced in database are missing on disk.");
                }

                documentsReconciled = missingFiles == 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed document reconciliation probe.");
            inconsistencies.Add($"Document reconciliation exception: {ex.Message}");
        }

        // 3. Data Protection Key Verification
        var keyRingAccessible = Directory.Exists(_keyRingPath);
        var protectUnprotectVerified = false;

        try
        {
            var protector = _dataProtectionProvider.CreateProtector("ConvoLab.RecoveryVerification");
            const string testPayload = "ConvoLab-Recovery-Verification-Token-2026";
            var protectedData = protector.Protect(testPayload);
            var unprotectedData = protector.Unprotect(protectedData);

            protectUnprotectVerified = string.Equals(testPayload, unprotectedData, StringComparison.Ordinal);
            if (!protectUnprotectVerified)
            {
                inconsistencies.Add("Data Protection roundtrip protect/unprotect failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed Data Protection verification probe.");
            inconsistencies.Add($"Data Protection cryptographic verification failed: {ex.Message}");
        }

        var isHealthy = canConnect && migrationsUpToDate && documentsReconciled && protectUnprotectVerified && inconsistencies.Count == 0;

        return new RecoveryVerificationResult(
            IsHealthy: isHealthy,
            VerifiedAt: DateTimeOffset.UtcNow,
            Database: new DatabaseVerificationSummary(
                CanConnect: canConnect,
                MigrationsUpToDate: migrationsUpToDate,
                UserCount: userCount,
                WorkspaceCount: workspaceCount,
                ExternalIdentityCount: externalIdentityCount,
                PolicyCount: policyCount,
                EvaluationCount: evaluationCount,
                TraceCount: traceCount,
                AnalyticsEventCount: analyticsEventCount),
            Documents: new DocumentReconciliationSummary(
                Reconciled: documentsReconciled,
                DatabaseDocumentCount: dbDocumentCount,
                PhysicalFileCount: physicalFileCount,
                MissingFiles: missingFiles,
                OrphanFiles: orphanFiles),
            DataProtection: new DataProtectionVerificationSummary(
                KeyRingAccessible: keyRingAccessible,
                ProtectUnprotectVerified: protectUnprotectVerified),
            Inconsistencies: inconsistencies);
    }
}
