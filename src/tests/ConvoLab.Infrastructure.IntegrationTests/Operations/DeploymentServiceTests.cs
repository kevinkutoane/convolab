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
            ReleaseVersion: "1.0.0-alpha.16",
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
            ReleaseVersion: "1.0.0-alpha.16",
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
            ReleaseVersion: "1.0.0-alpha.16",
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
}
