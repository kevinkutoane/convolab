using System;
using System.Linq;
using System.Threading.Tasks;
using ConvoLab.Application.Operations.Deployment;
using ConvoLab.Domain.Operations.Deployment;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Operations.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConvoLab.Infrastructure.IntegrationTests.Operations;

public sealed class DeploymentServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task RegisterCandidate_validates_manifest_binding_and_sets_initial_status()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeploymentService(db, NullLogger<DeploymentService>.Instance);

        var manifest = new ReleaseManifest(
            ReleaseManifestId: "manifest-v1-abc",
            ReleaseVersion: "1.0.0-alpha.17",
            SourceCommitSha: "a1b2c3d4e5f6",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:1111111111111111111111111111111111111111111111111111111111111111",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:2222222222222222222222222222222222222222222222222222222222222222",
            MigrationVersion: "202608200002_DeploymentPromotionV1",
            ApiSbomSha256: "3333333333333333333333333333333333333333333333333333333333333333",
            StudioSbomSha256: "4444444444444444444444444444444444444444444444444444444444444444",
            ProvenanceReference: "https://github.com/convolab/actions/runs/100",
            BuildWorkflowId: "100",
            BuildTimestamp: DateTimeOffset.UtcNow);

        // Production candidates start as Pending approval
        var prodRecord = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifest, "Production"));
        Assert.Equal(DeploymentStatus.Pending, prodRecord.Status);
        Assert.Equal("manifest-v1-abc", prodRecord.ReleaseManifestId);

        // UAT candidates start as Approved for deployment
        var uatRecord = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifest, "UAT"));
        Assert.Equal(DeploymentStatus.Approved, uatRecord.Status);
    }

    [Fact]
    public async Task ApproveDeployment_transitions_pending_record_to_approved()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeploymentService(db, NullLogger<DeploymentService>.Instance);

        var manifest = new ReleaseManifest(
            ReleaseManifestId: "manifest-v1-prod",
            ReleaseVersion: "1.0.0-alpha.17",
            SourceCommitSha: "a1b2c3d4e5f6",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:1111",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:2222",
            MigrationVersion: null,
            ApiSbomSha256: null,
            StudioSbomSha256: null,
            ProvenanceReference: null,
            BuildWorkflowId: "101",
            BuildTimestamp: DateTimeOffset.UtcNow);

        var prodRecord = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifest, "Production"));
        Assert.Equal(DeploymentStatus.Pending, prodRecord.Status);

        var approved = await service.ApproveDeploymentAsync(prodRecord.Id, new ApprovePromotionRequest("platform-admin@convolab.io", "Verified in UAT"));
        Assert.Equal(DeploymentStatus.Approved, approved.Status);
        Assert.Equal("platform-admin@convolab.io", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task CompleteDeployment_records_health_and_smoke_evidence()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeploymentService(db, NullLogger<DeploymentService>.Instance);

        var manifest = new ReleaseManifest(
            ReleaseManifestId: "manifest-v1-smoke",
            ReleaseVersion: "1.0.0-alpha.17",
            SourceCommitSha: "a1b2c3d4e5f6",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:1111",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:2222",
            MigrationVersion: null,
            ApiSbomSha256: null,
            StudioSbomSha256: null,
            ProvenanceReference: null,
            BuildWorkflowId: "102",
            BuildTimestamp: DateTimeOffset.UtcNow);

        var record = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifest, "UAT"));
        await service.StartDeploymentAsync(record.Id);

        var completed = await service.CompleteDeploymentAsync(record.Id, new CompleteDeploymentRequest(
            IsHealthy: true,
            HealthSummary: "/health/ready 200 OK",
            SmokeTestSummary: "Deterministic simulation and auth verified."));

        Assert.Equal(DeploymentStatus.Healthy, completed.Status);
        Assert.Equal("/health/ready 200 OK", completed.HealthCheckSummary);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task PromoteCandidate_fails_if_source_manifest_not_healthy()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeploymentService(db, NullLogger<DeploymentService>.Instance);

        var request = new PromoteCandidateRequest(
            ReleaseManifestId: "unverified-manifest-1",
            SourceEnvironment: "UAT",
            TargetEnvironment: "Production",
            OperatorId: "release-manager@convolab.io");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PromoteCandidateAsync(request));
    }

    [Fact]
    public async Task PromoteCandidate_succeeds_when_verified_in_source_environment()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeploymentService(db, NullLogger<DeploymentService>.Instance);

        var manifest = new ReleaseManifest(
            ReleaseManifestId: "manifest-v2-verified",
            ReleaseVersion: "1.0.0-rc.1",
            SourceCommitSha: "c0ffee123456",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:1111",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:2222",
            MigrationVersion: null,
            ApiSbomSha256: null,
            StudioSbomSha256: null,
            ProvenanceReference: null,
            BuildWorkflowId: "201",
            BuildTimestamp: DateTimeOffset.UtcNow);

        var uatRecord = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifest, "UAT"));
        await service.StartDeploymentAsync(uatRecord.Id);
        await service.CompleteDeploymentAsync(uatRecord.Id, new CompleteDeploymentRequest(true, "All green"));

        var promoteRequest = new PromoteCandidateRequest(
            ReleaseManifestId: "manifest-v2-verified",
            SourceEnvironment: "UAT",
            TargetEnvironment: "Production",
            OperatorId: "release-lead@convolab.io",
            Reason: "Signed off in UAT testing.");

        var prodCandidate = await service.PromoteCandidateAsync(promoteRequest);

        Assert.Equal("Production", prodCandidate.Environment);
        Assert.Equal(DeploymentStatus.Pending, prodCandidate.Status);
        Assert.Equal("manifest-v2-verified", prodCandidate.ReleaseManifestId);
    }

    [Fact]
    public async Task RollbackDeployment_transitions_to_rolled_back_and_restores_previous_stable_manifest()
    {
        var db = CreateInMemoryDbContext();
        var service = new DeploymentService(db, NullLogger<DeploymentService>.Instance);

        // Step 1: Deploy stable v1
        var manifestV1 = new ReleaseManifest(
            ReleaseManifestId: "manifest-v1-stable",
            ReleaseVersion: "1.0.0",
            SourceCommitSha: "a1b2c3d4e5f6",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:1111",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:2222",
            MigrationVersion: null,
            ApiSbomSha256: null,
            StudioSbomSha256: null,
            ProvenanceReference: null,
            BuildWorkflowId: "301",
            BuildTimestamp: DateTimeOffset.UtcNow);

        var rec1 = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifestV1, "Production"));
        await service.ApproveDeploymentAsync(rec1.Id, new ApprovePromotionRequest("lead@convolab.io", "Approved"));
        await service.StartDeploymentAsync(rec1.Id);
        await service.CompleteDeploymentAsync(rec1.Id, new CompleteDeploymentRequest(true, "V1 Healthy"));

        // Step 2: Deploy buggy v2
        var manifestV2 = new ReleaseManifest(
            ReleaseManifestId: "manifest-v2-faulty",
            ReleaseVersion: "1.1.0",
            SourceCommitSha: "b2c3d4e5f6a1",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:3333",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:4444",
            MigrationVersion: null,
            ApiSbomSha256: null,
            StudioSbomSha256: null,
            ProvenanceReference: null,
            BuildWorkflowId: "302",
            BuildTimestamp: DateTimeOffset.UtcNow);

        var rec2 = await service.RegisterCandidateAsync(new RegisterCandidateRequest(manifestV2, "Production"));
        Assert.Equal("manifest-v1-stable", rec2.PreviousReleaseManifestId);
        await service.ApproveDeploymentAsync(rec2.Id, new ApprovePromotionRequest("lead@convolab.io", "Approved"));
        await service.StartDeploymentAsync(rec2.Id);
        await service.CompleteDeploymentAsync(rec2.Id, new CompleteDeploymentRequest(false, "Crash loop detected", FailureReason: "Unhandled exception in background worker"));

        // Step 3: Trigger rollback
        var rollbackResult = await service.RollbackDeploymentAsync(rec2.Id, new RollbackDeploymentRequest("operator@convolab.io", "High error rate"));

        Assert.Equal(DeploymentStatus.RolledBack, rollbackResult.Status);

        // Verify recovery record was automatically created restoring v1
        var allDeployments = await service.ListDeploymentsAsync("Production");
        var activeRecovery = allDeployments.First();

        Assert.Equal(DeploymentStatus.Healthy, activeRecovery.Status);
        Assert.Equal("manifest-v1-stable", activeRecovery.ReleaseManifestId);
        Assert.Equal("manifest-v2-faulty", activeRecovery.PreviousReleaseManifestId);
    }
}
