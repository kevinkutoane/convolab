using System;

namespace ConvoLab.Domain.Operations.Deployment;

public enum DeploymentStatus
{
    Pending,
    Approved,
    Deploying,
    Healthy,
    Failed,
    RolledBack,
    Cancelled
}

public sealed class DeploymentRecord
{
    public Guid Id { get; private set; }
    public string ReleaseManifestId { get; private set; } = string.Empty;
    public string ReleaseVersion { get; private set; } = string.Empty;
    public string SourceCommitSha { get; private set; } = string.Empty;
    public string ApiImageDigest { get; private set; } = string.Empty;
    public string StudioImageDigest { get; private set; } = string.Empty;
    public string? MigrationVersion { get; private set; }
    public string? SbomSha256 { get; private set; }
    public string? ProvenanceReference { get; private set; }
    public string Environment { get; private set; } = string.Empty;
    public DeploymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovalReason { get; private set; }
    public string? BackupIdBeforeMigration { get; private set; }
    public string? HealthCheckSummary { get; private set; }
    public string? SmokeTestSummary { get; private set; }
    public string? FailureReason { get; private set; }
    public string? PreviousReleaseManifestId { get; private set; }

    private DeploymentRecord() { }

    public static DeploymentRecord Create(
        string releaseManifestId,
        string releaseVersion,
        string sourceCommitSha,
        string apiImageDigest,
        string studioImageDigest,
        string? migrationVersion,
        string? sbomSha256,
        string? provenanceReference,
        string environment,
        string? previousReleaseManifestId = null)
    {
        if (string.IsNullOrWhiteSpace(releaseManifestId)) throw new ArgumentException("ReleaseManifestId is required.", nameof(releaseManifestId));
        if (string.IsNullOrWhiteSpace(releaseVersion)) throw new ArgumentException("ReleaseVersion is required.", nameof(releaseVersion));
        if (string.IsNullOrWhiteSpace(sourceCommitSha)) throw new ArgumentException("SourceCommitSha is required.", nameof(sourceCommitSha));
        if (string.IsNullOrWhiteSpace(apiImageDigest)) throw new ArgumentException("ApiImageDigest is required.", nameof(apiImageDigest));
        if (string.IsNullOrWhiteSpace(studioImageDigest)) throw new ArgumentException("StudioImageDigest is required.", nameof(studioImageDigest));
        if (string.IsNullOrWhiteSpace(environment)) throw new ArgumentException("Environment is required.", nameof(environment));

        return new DeploymentRecord
        {
            Id = Guid.NewGuid(),
            ReleaseManifestId = releaseManifestId.Trim(),
            ReleaseVersion = releaseVersion.Trim(),
            SourceCommitSha = sourceCommitSha.Trim(),
            ApiImageDigest = apiImageDigest.Trim(),
            StudioImageDigest = studioImageDigest.Trim(),
            MigrationVersion = migrationVersion?.Trim(),
            SbomSha256 = sbomSha256?.Trim(),
            ProvenanceReference = provenanceReference?.Trim(),
            Environment = environment.Trim(),
            Status = environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
                ? DeploymentStatus.Pending
                : DeploymentStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow,
            PreviousReleaseManifestId = previousReleaseManifestId?.Trim()
        };
    }

    public void Approve(string operatorId, string reason)
    {
        if (string.IsNullOrWhiteSpace(operatorId)) throw new ArgumentException("Operator is required for approval.", nameof(operatorId));
        if (Status != DeploymentStatus.Pending) throw new InvalidOperationException($"Deployment is in state '{Status}' and cannot be approved.");

        ApprovedBy = operatorId.Trim();
        ApprovalReason = reason?.Trim();
        ApprovedAt = DateTimeOffset.UtcNow;
        Status = DeploymentStatus.Approved;
    }

    public void MarkDeploying(string? backupIdBeforeMigration = null)
    {
        if (Status != DeploymentStatus.Approved) throw new InvalidOperationException($"Cannot deploy a record in state '{Status}'. Must be Approved.");
        StartedAt = DateTimeOffset.UtcNow;
        BackupIdBeforeMigration = backupIdBeforeMigration;
        Status = DeploymentStatus.Deploying;
    }

    public void MarkHealthy(string healthSummary, string? smokeSummary = null)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        HealthCheckSummary = healthSummary;
        SmokeTestSummary = smokeSummary;
        Status = DeploymentStatus.Healthy;
    }

    public void MarkFailed(string failureReason)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        FailureReason = failureReason;
        Status = DeploymentStatus.Failed;
    }

    public void MarkRolledBack(string reason)
    {
        CompletedAt = DateTimeOffset.UtcNow;
        FailureReason = reason;
        Status = DeploymentStatus.RolledBack;
    }
}
