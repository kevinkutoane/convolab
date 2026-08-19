using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConvoLab.Application.Operations.Backups;

public sealed record RecoveryVerificationResult(
    bool IsHealthy,
    DateTimeOffset VerifiedAt,
    DatabaseVerificationSummary Database,
    DocumentReconciliationSummary Documents,
    DataProtectionVerificationSummary DataProtection,
    IReadOnlyList<string> Inconsistencies);

public sealed record DatabaseVerificationSummary(
    bool CanConnect,
    bool MigrationsUpToDate,
    long UserCount,
    long WorkspaceCount,
    long ExternalIdentityCount,
    long PolicyCount,
    long EvaluationCount,
    long TraceCount,
    long AnalyticsEventCount);

public sealed record DocumentReconciliationSummary(
    bool Reconciled,
    long DatabaseDocumentCount,
    long PhysicalFileCount,
    long MissingFiles,
    long OrphanFiles);

public sealed record DataProtectionVerificationSummary(
    bool KeyRingAccessible,
    bool ProtectUnprotectVerified);

public interface IRecoveryVerifier
{
    Task<RecoveryVerificationResult> VerifyRecoveryAsync(CancellationToken cancellationToken = default);
}
