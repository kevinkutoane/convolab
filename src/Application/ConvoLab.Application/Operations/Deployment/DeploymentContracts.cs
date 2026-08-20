using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Domain.Operations.Deployment;

namespace ConvoLab.Application.Operations.Deployment;

public sealed record ReleaseManifest(
    string ReleaseManifestId,
    string ReleaseVersion,
    string SourceCommitSha,
    string ApiImageDigest,
    string StudioImageDigest,
    string? MigrationVersion,
    string? SbomSha256,
    string? ProvenanceReference,
    string BuildWorkflowId,
    DateTimeOffset BuildTimestamp,
    bool IsBackwardCompatible = true,
    bool RequiresDowntime = false);

public sealed record RegisterCandidateRequest(
    ReleaseManifest Manifest,
    string TargetEnvironment);

public sealed record ApprovePromotionRequest(
    string OperatorId,
    string Reason);

public sealed record CompleteDeploymentRequest(
    bool IsHealthy,
    string HealthSummary,
    string? SmokeTestSummary = null,
    string? FailureReason = null);

public sealed record EnvironmentDeploymentState(
    string Environment,
    string? ActiveReleaseManifestId,
    string? ActiveReleaseVersion,
    string? ActiveApiDigest,
    string? ActiveStudioDigest,
    string? ActiveMigrationVersion,
    DeploymentStatus CurrentStatus,
    DateTimeOffset? LastDeployedAt,
    string? LastBackupId);

public interface IDeploymentService
{
    Task<DeploymentRecord> RegisterCandidateAsync(RegisterCandidateRequest request, CancellationToken cancellationToken = default);
    Task<DeploymentRecord> ApproveDeploymentAsync(Guid deploymentId, ApprovePromotionRequest request, CancellationToken cancellationToken = default);
    Task<DeploymentRecord> StartDeploymentAsync(Guid deploymentId, string? backupIdBeforeMigration = null, CancellationToken cancellationToken = default);
    Task<DeploymentRecord> CompleteDeploymentAsync(Guid deploymentId, CompleteDeploymentRequest request, CancellationToken cancellationToken = default);
    Task<DeploymentRecord?> GetDeploymentAsync(Guid deploymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeploymentRecord>> ListDeploymentsAsync(string? environment = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EnvironmentDeploymentState>> GetEnvironmentStatesAsync(CancellationToken cancellationToken = default);
}
