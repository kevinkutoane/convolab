using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        Assert.Matches("\\\"id\\\":\\\"tracing\\\"[^}]+\\\"status\\\":\\\"stable\\\"[^}]+\\\"version\\\":\\\"1.0\\\"", payload);
        Assert.Matches("\\\"id\\\":\\\"replay\\\"[^}]+\\\"status\\\":\\\"stable\\\"[^}]+\\\"version\\\":\\\"1.0\\\"", payload);
        Assert.Matches("\\\"id\\\":\\\"policy\\\"[^}]+\\\"status\\\":\\\"stable\\\"[^}]+\\\"version\\\":\\\"1.0\\\"", payload);
        Assert.Matches("\\\"id\\\":\\\"plugins\\\"[^}]+\\\"status\\\":\\\"stable\\\"[^}]+\\\"version\\\":\\\"1.0\\\"", payload);
    }

    [Fact]
    public async Task Plugin_registry_supports_registration_health_versioning_and_safe_activation_failure()
    {
        var overviewResponse = await _client.GetAsync("/api/plugins/overview");
        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        var overviewPayload = await overviewResponse.Content.ReadAsStringAsync();
        Assert.Contains("ConvoLab Deterministic Provider", overviewPayload);
        Assert.Contains("Persistent Trace Exporter", overviewPayload);

        var key = $"contract-plugin-{Guid.NewGuid():N}";
        var registerResponse = await _client.PostAsJsonAsync("/api/plugins", new
        {
            key,
            name = "Contract test plugin",
            description = "API lifecycle acceptance",
            publisher = "ConvoLab tests",
            version = "1.0.0",
            category = "Tool",
            manifestUrl = "builtin://untrusted/contract-test",
            entryPoint = "ContractTestPlugin",
            platformApiVersion = "1.0",
            capabilities = new[] { "contract-test" },
            permissions = Array.Empty<string>(),
            configurationSchema = "{}",
            metadata = new Dictionary<string, string> { ["source"] = "api-tests" }
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await ReadJsonAsync(registerResponse);
        var pluginId = registered.RootElement.GetProperty("summary").GetProperty("id").GetGuid();
        Assert.Equal("Installed", registered.RootElement.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal("Unknown", registered.RootElement.GetProperty("summary").GetProperty("healthStatus").GetString());

        var healthResponse = await _client.PostAsync($"/api/plugins/{pluginId}/health", null);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        var health = await ReadJsonAsync(healthResponse);
        Assert.Equal("Unhealthy", health.RootElement.GetProperty("status").GetString());
        Assert.Equal("BuiltInRegistry", health.RootElement.GetProperty("source").GetString());

        var activateResponse = await _client.PostAsync($"/api/plugins/{pluginId}/activate", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, activateResponse.StatusCode);
        Assert.Contains("plugin.lifecycle.invalid_transition", await activateResponse.Content.ReadAsStringAsync());

        var detailResponse = await _client.GetAsync($"/api/plugins/{pluginId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await ReadJsonAsync(detailResponse);
        var revision = detail.RootElement.GetProperty("summary").GetProperty("revision").GetInt64();
        var versionResponse = await _client.PostAsJsonAsync($"/api/plugins/{pluginId}/versions", new
        {
            version = "1.1.0",
            manifestUrl = "https://plugins.example.test/contract-test.json",
            revision
        });
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var version = await ReadJsonAsync(versionResponse);
        Assert.Equal("1.1.0", version.RootElement.GetProperty("summary").GetProperty("version").GetString());
        Assert.Equal("Installed", version.RootElement.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(2, version.RootElement.GetProperty("versionHistory").GetArrayLength());
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

    [Fact]
    public async Task Expanded_scorecard_routes_support_detail_publish_and_version_conflicts()
    {
        var name = $"Expanded release gate {Guid.NewGuid():N}";
        var request = new
        {
            name,
            description = "Versioned API acceptance scorecard",
            version = "1.0",
            qualityGateThreshold = .87,
            isDefault = false
        };

        var create = await _client.PostAsJsonAsync("/api/evaluations/scorecards", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await ReadJsonAsync(create);
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.RootElement.GetProperty("status").GetString());

        var detail = await _client.GetAsync($"/api/evaluations/scorecards/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains(name, await detail.Content.ReadAsStringAsync());

        var publish = await _client.PostAsync($"/api/evaluations/scorecards/{id}/publish?revision=1", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Contains("\"status\":\"Published\"", await publish.Content.ReadAsStringAsync());

        var duplicate = await _client.PostAsJsonAsync("/api/evaluations/scorecards", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("evaluation.scorecard.version_conflict", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Governed_execution_flows_through_evaluation_trace_replay_and_denies_before_provider_invocation()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/policies/overview")).StatusCode);

        var simulationResponse = await _client.PostAsJsonAsync("/api/simulations", new
        {
            title = $"Capability acceptance {Guid.NewGuid():N}",
            workflow = "Demo Claims Intake v1.0",
            promptVersion = "Demo Claims Assistant v1.0",
            knowledgeCollection = "Demo Claims Knowledge"
        });
        Assert.Equal(HttpStatusCode.Created, simulationResponse.StatusCode);
        var simulation = await ReadJsonAsync(simulationResponse);
        var simulationId = simulation.RootElement.GetProperty("id").GetGuid();

        var baselineResponse = await _client.PostAsJsonAsync($"/api/simulations/{simulationId}/messages", new
        {
            content = "My vehicle was damaged by hail. Explain the governed claims process.",
            provider = "Deterministic",
            model = "convolab-deterministic-primary",
            temperature = .2,
            maxOutputTokens = 400,
            mode = "Normal"
        });
        Assert.Equal(HttpStatusCode.OK, baselineResponse.StatusCode);
        var baselineConversation = await ReadJsonAsync(baselineResponse);
        var baselineRun = baselineConversation.RootElement.GetProperty("runs").EnumerateArray().Last();
        var baselineRunId = baselineRun.GetProperty("id").GetGuid();
        Assert.Equal("Completed", baselineRun.GetProperty("status").GetString());
        Assert.Equal("ZAR", baselineRun.GetProperty("metrics").GetProperty("currency").GetString());

        var evaluations = await _client.GetAsync("/api/evaluations/runs");
        Assert.Equal(HttpStatusCode.OK, evaluations.StatusCode);
        Assert.Contains(baselineRunId.ToString(), await evaluations.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var traces = await _client.GetAsync($"/api/traces?query={baselineRunId}");
        Assert.Equal(HttpStatusCode.OK, traces.StatusCode);
        var traceList = await ReadJsonAsync(traces);
        var trace = Assert.Single(traceList.RootElement.EnumerateArray());
        var traceId = trace.GetProperty("id").GetGuid();
        Assert.Equal("ZAR", trace.GetProperty("currency").GetString());

        var redactedTrace = await _client.GetAsync($"/api/traces/{traceId}");
        Assert.Equal(HttpStatusCode.OK, redactedTrace.StatusCode);
        Assert.Contains("\"isRedacted\":true", await redactedTrace.Content.ReadAsStringAsync());
        var revealedTrace = await _client.GetAsync($"/api/traces/{traceId}?includeSensitive=true");
        Assert.Equal(HttpStatusCode.OK, revealedTrace.StatusCode);
        Assert.Contains("\"isRedacted\":false", await revealedTrace.Content.ReadAsStringAsync());

        var replayResponse = await _client.PostAsJsonAsync("/api/replay/experiments", new
        {
            name = $"Acceptance replay {Guid.NewGuid():N}",
            simulationId,
            sourceRunId = baselineRunId,
            candidateLabel = "Candidate A",
            provider = "Deterministic",
            model = "convolab-deterministic-primary",
            temperature = .3,
            maxOutputTokens = 420,
            mode = "Normal"
        });
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var replay = await ReadJsonAsync(replayResponse);
        var experimentId = replay.RootElement.GetProperty("summary").GetProperty("id").GetGuid();
        var candidate = Assert.Single(replay.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.Equal("ZAR", candidate.GetProperty("snapshot").GetProperty("currency").GetString());
        Assert.True(candidate.GetProperty("comparison").GetProperty("findings").GetArrayLength() > 0);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/replay/experiments/{experimentId}/complete", null)).StatusCode);
        var archive = await _client.PostAsync($"/api/replay/experiments/{experimentId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.Contains("\"status\":\"Archived\"", await archive.Content.ReadAsStringAsync());

        Guid denyPolicyId = Guid.Empty;
        try
        {
            var denyPolicyResponse = await _client.PostAsJsonAsync("/api/policies", new
            {
                name = $"Acceptance provider denial {Guid.NewGuid():N}",
                description = "Proves denials occur before provider invocation.",
                owner = "Acceptance suite",
                domain = "ProviderAccess",
                defaultEffect = "Allow",
                scope = "Global",
                environment = "All",
                tenantId = (Guid?)null,
                rules = new[]
                {
                    new
                    {
                        name = "Deny deterministic provider",
                        effect = "Deny",
                        priority = 1000,
                        match = new Dictionary<string, string> { ["provider"] = "deterministic" },
                        constraints = new Dictionary<string, string>()
                    }
                }
            });
            Assert.Equal(HttpStatusCode.Created, denyPolicyResponse.StatusCode);
            var denyPolicy = await ReadJsonAsync(denyPolicyResponse);
            denyPolicyId = denyPolicy.RootElement.GetProperty("summary").GetProperty("id").GetGuid();
            var activatePolicy = await _client.PostAsync($"/api/policies/{denyPolicyId}/activate", null);
            Assert.True(activatePolicy.IsSuccessStatusCode, await activatePolicy.Content.ReadAsStringAsync());

            var executionsBeforeDenial = await CountArrayAsync("/api/intelligence/executions?limit=500");
            var deniedResponse = await _client.PostAsJsonAsync($"/api/simulations/{simulationId}/messages", new
            {
                content = "This execution must be stopped by policy before the provider is called.",
                provider = "Deterministic",
                model = "convolab-deterministic-primary",
                temperature = .2,
                maxOutputTokens = 400,
                mode = "Normal"
            });
            Assert.Equal(HttpStatusCode.OK, deniedResponse.StatusCode);
            var deniedConversation = await ReadJsonAsync(deniedResponse);
            var deniedRun = deniedConversation.RootElement.GetProperty("runs").EnumerateArray().Last();
            var deniedRunId = deniedRun.GetProperty("id").GetGuid();
            Assert.Equal("Failed", deniedRun.GetProperty("status").GetString());
            Assert.Contains("deny", deniedRun.GetProperty("failureReason").GetString()!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(executionsBeforeDenial, await CountArrayAsync("/api/intelligence/executions?limit=500"));

            var decisions = await _client.GetAsync("/api/policies/decisions?limit=500");
            Assert.Equal(HttpStatusCode.OK, decisions.StatusCode);
            var decisionJson = await decisions.Content.ReadAsStringAsync();
            Assert.Contains(deniedRunId.ToString(), decisionJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"effect\":\"Deny\"", decisionJson);
            Assert.Contains("\"source\":\"ConversationSimulator\"", decisionJson);
        }
        finally
        {
            if (denyPolicyId != Guid.Empty)
                await _client.PostAsync($"/api/policies/{denyPolicyId}/retire", null);
        }
    }

    private async Task<int> CountArrayAsync(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await ReadJsonAsync(response);
        return document.RootElement.GetArrayLength();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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
