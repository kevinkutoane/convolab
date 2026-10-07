using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Application.Operations.Deployment;

namespace ConvoLab.Api.IntegrationTests;

public sealed class DeploymentAlmTests : IClassFixture<ConvoLabApiFactory>
{
    private readonly ConvoLabApiFactory _factory;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public DeploymentAlmTests(ConvoLabApiFactory factory, Xunit.Abstractions.ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@convolab.test",
            password = "Ephemeral-Alpha12!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var antiforgeryResponse = await client.GetAsync("/api/auth/antiforgery");
        var antiforgeryBody = await antiforgeryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var antiforgeryToken = antiforgeryBody.GetProperty("token").GetString();
        var antiforgeryHeaderName = antiforgeryBody.GetProperty("headerName").GetString();
        Assert.NotNull(antiforgeryHeaderName);
        Assert.NotNull(antiforgeryToken);
        client.DefaultRequestHeaders.Add(antiforgeryHeaderName, antiforgeryToken);

        return client;
    }

    [Fact]
    public async Task List_Deployments_Returns_Environment_States()
    {
        using var client = await CreateAdminClientAsync();
        var response = await client.GetAsync("/api/operations/deployments");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"ListDeployments ({response.StatusCode}): {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("environments", out var envs));
        Assert.True(envs.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task Deployment_Promotion_And_Rollback_Pipeline_Executes_Successfully()
    {
        using var client = await CreateAdminClientAsync();

        // 1. Register candidate for UAT
        var manifestId = $"manifest-api-{Guid.NewGuid():N}";
        var manifest = new ReleaseManifest(
            ReleaseManifestId: manifestId,
            ReleaseVersion: "2.0.0-rc.1",
            SourceCommitSha: "abcdef123456",
            ApiImageDigest: "ghcr.io/convolab/api@sha256:1111111111111111111111111111111111111111111111111111111111111111",
            StudioImageDigest: "ghcr.io/convolab/studio@sha256:2222222222222222222222222222222222222222222222222222222222222222",
            MigrationVersion: null,
            ApiSbomSha256: null,
            StudioSbomSha256: null,
            ProvenanceReference: null,
            BuildWorkflowId: "401",
            BuildTimestamp: DateTimeOffset.UtcNow);

        var regResponse = await client.PostAsJsonAsync("/api/operations/deployments/candidates", new RegisterCandidateRequest(manifest, "UAT"));
        var regBody = await regResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"RegisterCandidate ({regResponse.StatusCode}): {regBody}");
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var regJson = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        var uatId = regJson.GetProperty("id").GetGuid();

        // Complete UAT deployment as Healthy
        var completeResponse = await client.PostAsJsonAsync($"/api/operations/deployments/{uatId}/complete", new CompleteDeploymentRequest(true, "All UAT verification passed."));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // 2. Promote from UAT to Production
        var promoteRequest = new PromoteCandidateRequest(
            ReleaseManifestId: manifestId,
            SourceEnvironment: "UAT",
            TargetEnvironment: "Production",
            OperatorId: "platform-lead@convolab.io",
            Reason: "Production release approved.");

        var promoteResponse = await client.PostAsJsonAsync("/api/operations/deployments/promote", promoteRequest);
        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);

        var prodJson = await promoteResponse.Content.ReadFromJsonAsync<JsonElement>();
        var prodId = prodJson.GetProperty("id").GetGuid();
        Assert.Equal("Pending", prodJson.GetProperty("status").GetString());

        // 3. Approve and Complete Production
        var approveResponse = await client.PostAsJsonAsync($"/api/operations/deployments/{prodId}/approve", new ApprovePromotionRequest("ciso@convolab.io", "Security signoff granted"));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        await client.PostAsJsonAsync($"/api/operations/deployments/{prodId}/complete", new CompleteDeploymentRequest(true, "Production live."));

        // 4. Rollback
        var rollbackResponse = await client.PostAsJsonAsync($"/api/operations/deployments/{prodId}/rollback", new RollbackDeploymentRequest("ciso@convolab.io", "Canary metric regression"));
        Assert.Equal(HttpStatusCode.OK, rollbackResponse.StatusCode);

        var rollbackJson = await rollbackResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RolledBack", rollbackJson.GetProperty("status").GetString());
    }
}
