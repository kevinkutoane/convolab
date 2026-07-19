using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ConvoLab.Api.IntegrationTests;

public sealed class ApiContractTests : IClassFixture<ConvoLabApiFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(ConvoLabApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_Reports_Healthy()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Validation_Failure_Uses_Problem_Details()
    {
        var response = await _client.PostAsJsonAsync("/api/prompts", new
        {
            name = "",
            description = "",
            owner = "Kevin",
            category = "General",
            tags = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("prompt.name.required", payload);
        Assert.Contains("correlationId", payload);
    }

    [Fact]
    public async Task Intelligence_Overview_And_Plan_Preview_Are_Available()
    {
        var overviewResponse = await _client.GetAsync("/api/intelligence/overview");
        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        var overview = await overviewResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"providers\"", overview);
        Assert.Contains("ConvoLab Deterministic", overview);
        Assert.Contains("\"currency\":\"ZAR\"", overview);
        Assert.Contains("\"limit\":500", overview);

        var previewResponse = await _client.PostAsJsonAsync("/api/intelligence/plan-preview", new
        {
            provider = "Deterministic",
            model = "convolab-deterministic-primary",
            estimatedInputTokens = 1000,
            maxOutputTokens = 500,
            streaming = true,
            allowFallback = true,
            maxAttempts = 3,
            requiredCapabilities = new[] { "Chat", "TextGeneration" }
        });

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"estimatedTotalTokens\":1500", preview);
        Assert.Contains("\"estimatedCost\":0.04", preview);
        Assert.Contains("\"currency\":\"ZAR\"", preview);
        Assert.Contains("\"withinBudget\":true", preview);
    }

    [Fact]
    public async Task Invalid_Intelligence_Plan_Uses_Problem_Details()
    {
        var response = await _client.PostAsJsonAsync("/api/intelligence/plan-preview", new
        {
            provider = "Deterministic",
            model = "convolab-deterministic-primary",
            estimatedInputTokens = 0,
            maxOutputTokens = 0,
            streaming = false,
            allowFallback = false,
            maxAttempts = 0,
            requiredCapabilities = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("intelligence.plan.invalid", payload);
        Assert.Contains("estimatedInputTokens", payload);
    }

    [Fact]
    public async Task Evaluation_Overview_And_Preview_Are_Available()
    {
        var overviewResponse = await _client.GetAsync("/api/evaluation/overview");
        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        var overview = await overviewResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"minimumGroundedness\":0.8", overview);
        Assert.Contains("\"minimumSafety\":0.95", overview);

        var previewResponse = await _client.PostAsJsonAsync("/api/evaluation/preview", new
        {
            groundedness = .90,
            relevance = .88,
            safety = .99
        });

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"overallScore\":0.9155", preview);
        Assert.Contains("\"passed\":true", preview);
    }

    [Fact]
    public async Task Evaluation_Scorecard_Is_Persisted_And_Used_By_Preview()
    {
        var name = $"Release gate {Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync("/api/evaluation/scorecards", new
        {
            name,
            description = "API contract scorecard",
            minimumGroundedness = .95,
            minimumRelevance = .9,
            minimumSafety = .99,
            minimumOverallScore = .94,
            failureAction = "Block"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<EvaluationScorecardContract>();
        Assert.NotNull(created);

        var listResponse = await _client.GetAsync("/api/evaluation/scorecards");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(name, await listResponse.Content.ReadAsStringAsync());

        var previewResponse = await _client.PostAsJsonAsync("/api/evaluation/preview", new
        {
            groundedness = .9,
            relevance = .95,
            safety = .995,
            scorecardId = created.Id
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"verdict\":\"Block\"", preview);
        Assert.Contains("Groundedness", preview);
    }

    [Fact]
    public async Task Platform_Status_Reports_Evaluation_As_Stable()
    {
        var response = await _client.GetAsync("/api/platform/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Matches("\\\"id\\\":\\\"evaluation\\\"[^}]+\\\"status\\\":\\\"stable\\\"[^}]+\\\"version\\\":\\\"1.0\\\"", payload);
    }

    [Fact]
    public async Task Invalid_Evaluation_Preview_Uses_Problem_Details()
    {
        var response = await _client.PostAsJsonAsync("/api/evaluation/preview", new
        {
            groundedness = 2,
            relevance = .8,
            safety = .9
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("evaluation.preview.invalid", payload);
        Assert.Contains("groundedness", payload);
    }
}

public sealed record EvaluationScorecardContract(Guid Id);

public sealed class ConvoLabApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=convolab-api-tests.db",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Knowledge:StoragePath"] = Path.Combine(Path.GetTempPath(), "convolab-api-tests")
            });
        });
    }
}
