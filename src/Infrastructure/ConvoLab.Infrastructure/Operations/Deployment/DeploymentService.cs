using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Deployment;
using ConvoLab.Domain.Operations.Deployment;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Deployment;

internal sealed class DeploymentService : IDeploymentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(ApplicationDbContext dbContext, ILogger<DeploymentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DeploymentRecord> RegisterCandidateAsync(RegisterCandidateRequest request, CancellationToken cancellationToken = default)
    {
        var manifest = request.Manifest;

        // Verify binding of release manifest properties
        if (string.IsNullOrWhiteSpace(manifest.ReleaseManifestId)) throw new ArgumentException("ReleaseManifestId cannot be empty.");
        if (string.IsNullOrWhiteSpace(manifest.SourceCommitSha)) throw new ArgumentException("SourceCommitSha cannot be empty.");
        if (string.IsNullOrWhiteSpace(manifest.ApiImageDigest)) throw new ArgumentException("ApiImageDigest cannot be empty.");
        if (string.IsNullOrWhiteSpace(manifest.StudioImageDigest)) throw new ArgumentException("StudioImageDigest cannot be empty.");

        var previousCandidates = await _dbContext.DeploymentRecords
            .Where(d => d.Environment == request.TargetEnvironment && d.Status == DeploymentStatus.Healthy)
            .ToListAsync(cancellationToken);

        var previous = previousCandidates
            .OrderByDescending(d => d.CompletedAt ?? d.CreatedAt)
            .FirstOrDefault();

        var record = DeploymentRecord.Create(
            releaseManifestId: manifest.ReleaseManifestId,
            releaseVersion: manifest.ReleaseVersion,
            sourceCommitSha: manifest.SourceCommitSha,
            apiImageDigest: manifest.ApiImageDigest,
            studioImageDigest: manifest.StudioImageDigest,
            migrationVersion: manifest.MigrationVersion,
            sbomSha256: manifest.SbomSha256,
            provenanceReference: manifest.ProvenanceReference,
            environment: request.TargetEnvironment,
            previousReleaseManifestId: previous?.ReleaseManifestId);

        _dbContext.DeploymentRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered deployment candidate {DeploymentId} for manifest {ManifestId} on env {Environment}",
            record.Id, manifest.ReleaseManifestId, request.TargetEnvironment);

        return record;
    }

    public async Task<DeploymentRecord> ApproveDeploymentAsync(Guid deploymentId, ApprovePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.DeploymentRecords.FirstOrDefaultAsync(d => d.Id == deploymentId, cancellationToken)
                     ?? throw new InvalidOperationException($"Deployment record {deploymentId} not found.");

        record.Approve(request.OperatorId, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deployment {DeploymentId} approved by {Operator} for {Environment}",
            deploymentId, request.OperatorId, record.Environment);

        return record;
    }

    public async Task<DeploymentRecord> StartDeploymentAsync(Guid deploymentId, string? backupIdBeforeMigration = null, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.DeploymentRecords.FirstOrDefaultAsync(d => d.Id == deploymentId, cancellationToken)
                     ?? throw new InvalidOperationException($"Deployment record {deploymentId} not found.");

        record.MarkDeploying(backupIdBeforeMigration);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deployment {DeploymentId} transitioned to Deploying state", deploymentId);
        return record;
    }

    public async Task<DeploymentRecord> CompleteDeploymentAsync(Guid deploymentId, CompleteDeploymentRequest request, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.DeploymentRecords.FirstOrDefaultAsync(d => d.Id == deploymentId, cancellationToken)
                     ?? throw new InvalidOperationException($"Deployment record {deploymentId} not found.");

        if (request.IsHealthy)
        {
            record.MarkHealthy(request.HealthSummary, request.SmokeTestSummary);
            _logger.LogInformation("Deployment {DeploymentId} marked Healthy on {Environment}", deploymentId, record.Environment);
        }
        else
        {
            record.MarkFailed(request.FailureReason ?? "Health/smoke checks failed.");
            _logger.LogWarning("Deployment {DeploymentId} marked Failed on {Environment}: {Reason}", deploymentId, record.Environment, request.FailureReason);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task<DeploymentRecord?> GetDeploymentAsync(Guid deploymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeploymentRecords.FirstOrDefaultAsync(d => d.Id == deploymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<DeploymentRecord>> ListDeploymentsAsync(string? environment = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DeploymentRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(d => d.Environment == environment);
        }

        var all = await query.ToListAsync(cancellationToken);
        return all.OrderByDescending(d => d.CreatedAt).Take(limit).ToList();
    }

    public async Task<IReadOnlyList<EnvironmentDeploymentState>> GetEnvironmentStatesAsync(CancellationToken cancellationToken = default)
    {
        var envs = new[] { "Development", "UAT", "Production" };
        var list = new List<EnvironmentDeploymentState>();

        var allRecords = await _dbContext.DeploymentRecords.ToListAsync(cancellationToken);

        foreach (var env in envs)
        {
            var active = allRecords
                .Where(d => d.Environment == env)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();

            list.Add(new EnvironmentDeploymentState(
                Environment: env,
                ActiveReleaseManifestId: active?.ReleaseManifestId,
                ActiveReleaseVersion: active?.ReleaseVersion ?? "1.0.0-alpha.16",
                ActiveApiDigest: active?.ApiImageDigest,
                ActiveStudioDigest: active?.StudioImageDigest,
                ActiveMigrationVersion: active?.MigrationVersion,
                CurrentStatus: active?.Status ?? DeploymentStatus.Healthy,
                LastDeployedAt: active?.CompletedAt ?? active?.CreatedAt,
                LastBackupId: active?.BackupIdBeforeMigration));
        }

        return list;
    }
}
