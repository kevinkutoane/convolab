using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Api.Security;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Xunit.Abstractions;

namespace ConvoLab.Api.IntegrationTests;

public sealed class ApiContractTests : IClassFixture<ConvoLabApiFactory>
{
    private readonly HttpClient _client;
    private readonly ConvoLabApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public ApiContractTests(ConvoLabApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liveness_Reports_Healthy()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Local_login_issues_revocable_http_only_session_and_workspace_context()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email = "admin@convolab.test", password = "Ephemeral-Alpha12!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), value => value.Contains("convolab_session=", StringComparison.Ordinal) && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var payload = await ReadJsonAsync(login);
        Assert.Equal("admin@convolab.test", payload.RootElement.GetProperty("email").GetString());
        Assert.Equal("Default Workspace", payload.RootElement.GetProperty("workspaces")[0].GetProperty("name").GetString());
        Assert.NotEqual(Guid.Empty, payload.RootElement.GetProperty("activeWorkspaceId").GetGuid());
        var antiforgeryResponse = await _client.GetAsync("/api/auth/antiforgery");
        Assert.True(antiforgeryResponse.Headers.CacheControl?.NoStore);
        Assert.Contains(antiforgeryResponse.Headers.GetValues("Set-Cookie"), value =>
            value.Contains(ConvoLabAuthentication.AntiforgeryCookie, StringComparison.Ordinal)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var antiforgery = await ReadJsonAsync(antiforgeryResponse);
        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refresh.Headers.Add("X-XSRF-TOKEN", antiforgery.RootElement.GetProperty("token").GetString());
        var refreshed = await _client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains(refreshed.Headers.GetValues("Set-Cookie"), value => value.Contains("convolab_session=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Service_account_scope_allows_reads_and_rejects_mutations()
    {
        var workspaces = await ReadJsonAsync(await _client.GetAsync("/api/workspaces"));
        var workspaceId = workspaces.RootElement[0].GetProperty("id").GetGuid();
        var created = await _client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/service-accounts", new
        {
            name = $"Viewer bot {Guid.NewGuid():N}", scopes = new[] { "WorkspaceMember" }, expiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdPayload = await ReadJsonAsync(created);
        var accountId = createdPayload.RootElement.GetProperty("id").GetGuid();
        var credential = createdPayload.RootElement.GetProperty("credential").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/prompts");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request)).StatusCode);
        using var mutation = new HttpRequestMessage(HttpMethod.Post, "/api/simulations") { Content = JsonContent.Create(new { title = "Forbidden", workflow = "Claims intake", promptVersion = "1.0.0", knowledgeCollection = "Claims" }) };
        mutation.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(mutation)).StatusCode);

        const string leadingUnderscoreSecret = "_leading-underscore-secret";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await db.ServiceAccounts.FindAsync(accountId);
            Assert.NotNull(account);
            account.SecretHash = ConvoLabAuthentication.HashSecret(leadingUnderscoreSecret);
            await db.SaveChangesAsync();
        }
        using var leadingUnderscoreRequest = new HttpRequestMessage(HttpMethod.Get, "/api/prompts");
        leadingUnderscoreRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", $"clsa_{accountId:N}_{leadingUnderscoreSecret}");
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(leadingUnderscoreRequest)).StatusCode);
    }

    [Fact]
    public async Task Tenant_routes_reject_real_workspace_environment_and_organisation_ids_from_another_context()
    {
        var foreignOrganisationId = Guid.NewGuid();
        var foreignWorkspaceId = Guid.NewGuid();
        var foreignEnvironmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Organisations.Add(new OrganisationRecord
            {
                Id = foreignOrganisationId,
                Name = "Foreign Organisation",
                Slug = $"foreign-{foreignOrganisationId:N}",
                Status = "Active",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.Workspaces.Add(new WorkspaceRecord
            {
                Id = foreignWorkspaceId,
                OrganisationId = foreignOrganisationId,
                Name = "Foreign Workspace",
                Slug = $"foreign-{foreignWorkspaceId:N}",
                Description = "Isolation-test workspace",
                Status = "Active",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.RuntimeEnvironments.Add(new RuntimeEnvironmentRecord
            {
                Id = foreignEnvironmentId,
                OrganisationId = foreignOrganisationId,
                WorkspaceId = foreignWorkspaceId,
                Name = "Foreign Environment",
                Slug = $"foreign-{foreignEnvironmentId:N}",
                EnvironmentType = "Development",
                Description = "Isolation-test environment",
                Status = "Active",
                IsDefault = true,
                CreatedAt = now,
                CreatedBy = WorkspaceIdentityDefaults.BootstrapUserId,
                UpdatedAt = now,
                Revision = 1
            });
            await db.SaveChangesAsync();
        }

        var foreignWorkspaceResponse = await _client.GetAsync($"/api/workspaces/{foreignWorkspaceId}/settings");
        Assert.Equal(HttpStatusCode.NotFound, foreignWorkspaceResponse.StatusCode);

        var foreignEnvironmentResponse = await _client.GetAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/environments/{foreignEnvironmentId}/settings");
        Assert.Equal(HttpStatusCode.NotFound, foreignEnvironmentResponse.StatusCode);

        var serviceAccount = await _client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/service-accounts",
            new
            {
                name = $"Isolation reader {Guid.NewGuid():N}",
                scopes = new[] { WorkspacePermissions.ViewSettings },
                expiresAt = now.AddHours(1)
            });
        Assert.Equal(HttpStatusCode.Created, serviceAccount.StatusCode);
        var credential = (await ReadJsonAsync(serviceAccount)).RootElement.GetProperty("credential").GetString();

        using var foreignOrganisationRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/organisations/{foreignOrganisationId}/settings");
        foreignOrganisationRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential);
        var foreignOrganisationResponse = await _client.SendAsync(foreignOrganisationRequest);
        Assert.Equal(HttpStatusCode.NotFound, foreignOrganisationResponse.StatusCode);
    }

    [Fact]
    public async Task Correlation_is_server_generated_and_client_value_is_only_parent_metadata()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "client-controlled-value");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.NotEqual("client-controlled-value", correlation);
        Assert.True(Guid.TryParseExact(correlation, "N", out _));
        Assert.Equal("client-controlled-value", Assert.Single(response.Headers.GetValues("X-Parent-Correlation-ID")));
    }

    [Fact]
    public async Task Effective_settings_expose_metadata_for_typed_studio_controls()
    {
        Guid environmentId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            environmentId = db.RuntimeEnvironments
                .Single(environment => environment.WorkspaceId == WorkspaceIdentityDefaults.WorkspaceId && environment.IsDefault)
                .Id;
        }

        var response = await _client.GetAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/environments/{environmentId}/settings/effective");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        var settings = payload.RootElement.EnumerateArray().ToArray();
        var provider = settings.Single(setting => setting.GetProperty("key").GetString() == "ai.provider");

        Assert.Contains("local repeatable test provider", provider.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Enum", provider.GetProperty("valueType").GetString());
        Assert.Equal(new[] { "Deterministic", "Gemini" }, provider.GetProperty("allowedValues").EnumerateArray().Select(item => item.GetString()));
        Assert.True(provider.GetProperty("allowsEnvironmentOverride").GetBoolean());
    }

    [Fact]
    public async Task Execution_environment_is_validated_and_defaulted_without_refresh()
    {
        using var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/simulations")
        {
            Content = JsonContent.Create(new
            {
                title = "Malformed environment",
                workflow = "Workflow",
                promptVersion = "Prompt",
                knowledgeCollection = "Knowledge"
            })
        };
        malformed.Headers.Add("X-ConvoLab-Environment-Id", "not-a-guid");
        var malformedResponse = await _client.SendAsync(malformed);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        Assert.Contains("runtime_environment.invalid", await malformedResponse.Content.ReadAsStringAsync());

        using var foreign = new HttpRequestMessage(HttpMethod.Post, "/api/simulations")
        {
            Content = JsonContent.Create(new
            {
                title = "Foreign environment",
                workflow = "Workflow",
                promptVersion = "Prompt",
                knowledgeCollection = "Knowledge"
            })
        };
        foreign.Headers.Add("X-ConvoLab-Environment-Id", Guid.NewGuid().ToString());
        var foreignResponse = await _client.SendAsync(foreign);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Contains("environment.not_found", await foreignResponse.Content.ReadAsStringAsync());

        var defaulted = await _client.PostAsJsonAsync("/api/simulations", new
        {
            title = "Default runtime environment",
            workflow = "Workflow",
            promptVersion = "Prompt",
            knowledgeCollection = "Knowledge"
        });
        Assert.Equal(HttpStatusCode.Created, defaulted.StatusCode);
        var resolved = Assert.Single(defaulted.Headers.GetValues("X-ConvoLab-Resolved-Environment-Id"));
        Assert.True(Guid.TryParse(resolved, out var resolvedId));

        var selected = await _client.PostAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/environments/{resolvedId}/select",
            null);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);

        var analytics = await _client.GetAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/overview?environmentId={resolvedId}");
        Assert.Equal(HttpStatusCode.OK, analytics.StatusCode);
        Assert.Contains("\"category\":\"overview\"", await analytics.Content.ReadAsStringAsync());

        var filterOptions = await _client.GetAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/filter-options?environmentId={resolvedId}");
        var filterOptionsPayload = await filterOptions.Content.ReadAsStringAsync();
        Assert.True(
            filterOptions.StatusCode == HttpStatusCode.OK,
            $"Expected analytics filter options, received {(int)filterOptions.StatusCode}: {filterOptionsPayload}");
        Assert.Contains("\"providers\"", filterOptionsPayload);
        Assert.Contains("\"eventTypes\"", filterOptionsPayload);
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

            IReadOnlyList<AnalyticsEventRecord> baselineEvents;
            IReadOnlyList<AnalyticsEventRecord> deniedEvents;
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var eventPayloads = await db.AnalyticsOutbox.AsNoTracking()
                    .Select(item => item.PayloadJson)
                    .ToListAsync();
                var emittedEvents = eventPayloads
                    .Select(payload => JsonSerializer.Deserialize<AnalyticsEventRecord>(
                        payload,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)))
                    .OfType<AnalyticsEventRecord>()
                    .ToList();
                baselineEvents = emittedEvents
                    .Where(item => item.SourceExecutionId == baselineRunId)
                    .ToList();
                deniedEvents = emittedEvents
                    .Where(item => item.SourceExecutionId == deniedRunId)
                    .ToList();
            }

            Assert.Single(baselineEvents, item => item.EventType == "SimulationCompleted");
            Assert.Single(deniedEvents, item => item.EventType == "SimulationFailed");
            var baselineRevision = Assert.Single(baselineEvents.Select(item => item.ConfigurationRevision).Distinct());
            var baselineCorrelation = Assert.Single(baselineEvents.Select(item => item.CorrelationId).Distinct());
            var prevented = Assert.Single(
                deniedEvents,
                item => item.EventType == "ProviderInvocationPrevented");
            Assert.True(prevented.ProviderInvocationPrevented);
            Assert.Equal(0, prevented.InputTokens);
            Assert.Equal(0, prevented.OutputTokens);
            Assert.Equal(0m, prevented.CostZar);
            Assert.Equal("Denied", prevented.PolicyOutcome);
            Assert.DoesNotContain(deniedEvents, item => item.EventType == "ProviderInvocationCompleted");

            _output.WriteLine(
                "Allowed reconciliation: SourceExecutionId={0}; CorrelationId={1}; OrganisationId={2}; WorkspaceId={3}; EnvironmentId={4}; ActorId={5}; ConfigurationRevision={6}; Provider={7}; Model={8}; InputTokens={9}; OutputTokens={10}; CostZar={11}; CostType={12}; EventIds={13}",
                baselineRunId,
                baselineCorrelation,
                baselineEvents[0].OrganisationId,
                baselineEvents[0].WorkspaceId,
                baselineEvents[0].EnvironmentId,
                baselineEvents[0].ActorId,
                baselineRevision,
                baselineEvents.FirstOrDefault(item => item.EventType == "ProviderInvocationCompleted")?.Provider,
                baselineEvents.FirstOrDefault(item => item.EventType == "ProviderInvocationCompleted")?.Model,
                baselineEvents.FirstOrDefault(item => item.EventType == "ProviderInvocationCompleted")?.InputTokens,
                baselineEvents.FirstOrDefault(item => item.EventType == "ProviderInvocationCompleted")?.OutputTokens,
                baselineEvents.FirstOrDefault(item => item.EventType == "ProviderInvocationCompleted")?.CostZar,
                baselineEvents.FirstOrDefault(item => item.EventType == "ProviderInvocationCompleted")?.CostType,
                string.Join(",", baselineEvents.Select(item => item.Id)));
            _output.WriteLine(
                "Denied reconciliation: SourceExecutionId={0}; CorrelationId={1}; OrganisationId={2}; WorkspaceId={3}; EnvironmentId={4}; ActorId={5}; ConfigurationRevision={6}; ProviderPrevented={7}; InputTokens={8}; OutputTokens={9}; CostZar={10}; PolicyOutcome={11}; EventIds={12}",
                deniedRunId,
                Assert.Single(deniedEvents.Select(item => item.CorrelationId).Distinct()),
                deniedEvents[0].OrganisationId,
                deniedEvents[0].WorkspaceId,
                deniedEvents[0].EnvironmentId,
                deniedEvents[0].ActorId,
                Assert.Single(deniedEvents.Select(item => item.ConfigurationRevision).Distinct()),
                prevented.ProviderInvocationPrevented,
                prevented.InputTokens,
                prevented.OutputTokens,
                prevented.CostZar,
                prevented.PolicyOutcome,
                string.Join(",", deniedEvents.Select(item => item.Id)));
        }
        finally
        {
            if (denyPolicyId != Guid.Empty)
                await _client.PostAsync($"/api/policies/{denyPolicyId}/retire", null);
        }
    }

    [Fact]
    public async Task Authentication_options_report_local_mode_without_exposing_oidc_configuration()
    {
        var response = await _client.GetAsync("/api/auth/options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"mode\":\"Local\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"localLoginAvailable\":true", payload, StringComparison.Ordinal);
        Assert.Contains("\"entraLoginAvailable\":false", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("tenantId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authority", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lock", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Operations_authentication_exposes_sanitized_Entra_identity_session_and_break_glass_evidence()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@convolab.test",
            password = "Ephemeral-Alpha12!"
        })).StatusCode);
        var response = await client.GetAsync("/api/operations/authentication");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var properties = payload.RootElement.EnumerateObject().Select(item => item.Name).OrderBy(item => item).ToArray();
        Assert.Equal([
            "activeSessions", "breakGlassAvailable", "breakGlassEnabled", "breakGlassFailuresLast24Hours",
            "breakGlassState", "breakGlassUsesLast24Hours", "clientAuthentication", "correlationId",
            "entraEnabled", "externalIdentityCount", "externalLoginFailuresLast24Hours",
            "externalLoginSuccessesLast24Hours", "lastBreakGlassSuccessfulUseAt", "lastFailureCode",
            "lastValidationAt", "linkedActiveUsers", "localLoginEnabled", "mode", "state",
            "tenantConfigurationState"
        ], properties);
        Assert.Equal("Local", payload.RootElement.GetProperty("mode").GetString());
        Assert.True(payload.RootElement.GetProperty("localLoginEnabled").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("entraEnabled").GetBoolean());
        Assert.Equal("NotConfigured", payload.RootElement.GetProperty("tenantConfigurationState").GetString());
        Assert.False(payload.RootElement.GetProperty("clientAuthentication").GetProperty("configured").GetBoolean());
        Assert.Equal("NotConfigured", payload.RootElement.GetProperty("state").GetString());
        Assert.True(payload.RootElement.GetProperty("activeSessions").GetInt32() >= 1);
        Assert.True(payload.RootElement.TryGetProperty("externalIdentityCount", out _));
        Assert.True(payload.RootElement.TryGetProperty("linkedActiveUsers", out _));
        Assert.True(payload.RootElement.TryGetProperty("externalLoginSuccessesLast24Hours", out _));
        Assert.True(payload.RootElement.TryGetProperty("externalLoginFailuresLast24Hours", out _));
        Assert.True(payload.RootElement.TryGetProperty("breakGlassUsesLast24Hours", out _));
        Assert.True(payload.RootElement.TryGetProperty("breakGlassFailuresLast24Hours", out _));
        var serialized = payload.RootElement.GetRawText();
        Assert.DoesNotContain("email", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenantId", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authority", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretReference", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subject", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/analytics?range=day")]
    public void Local_return_urls_are_accepted(string value) => Assert.True(EntraAuthentication.IsSafeReturnUrl(value));

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/%2f%2fevil.example")]
    [InlineData("/%252f%252fevil.example")]
    [InlineData("/%5cevil.example")]
    [InlineData("/safe%0d%0aLocation:%20https://evil.example")]
    [InlineData("javascript:alert(1)")]
    public void External_or_encoded_return_urls_are_rejected(string value) => Assert.False(EntraAuthentication.IsSafeReturnUrl(value));

    [Fact]
    public async Task Operations_polling_is_not_audited_but_readiness_and_safe_mode_mutations_are()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@convolab.test",
            password = "Ephemeral-Alpha12!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        long Baseline(string action)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return db.WorkspaceAuditEvents.LongCount(item => item.Action == action);
        }

        var readinessBefore = Baseline("Operations.ReadinessEvidenceViewed");
        for (var index = 0; index < 3; index++)
        {
            var statusResponse = await _client.GetAsync("/api/operations/status");
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            var status = await ReadJsonAsync(statusResponse);
            Assert.Equal("1.0.0-alpha.17", status.RootElement.GetProperty("version").GetString());
            Assert.Equal(
                "alpha.17 — Deployment, Environment Promotion & Release Engineering",
                status.RootElement.GetProperty("workstream").GetString());
        }
        Assert.Equal(readinessBefore, Baseline("Operations.ReadinessEvidenceViewed"));

        var readinessResponse = await _client.GetAsync("/api/operations/readiness");
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        var readiness = await ReadJsonAsync(readinessResponse);
        var componentStates = readiness.RootElement.GetProperty("components")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("component").GetString()!,
                item => item.GetProperty("state").GetString()!);
        Assert.Equal("Configured", componentStates["production-configuration"]);
        Assert.Equal("StubValidated", componentStates["providers"]);
        Assert.Equal("LiveValidated", componentStates["database"]);
        Assert.Equal(readinessBefore + 1, Baseline("Operations.ReadinessEvidenceViewed"));

        var summarizedReadiness = await ReadJsonAsync(
            await _client.GetAsync("/api/operations/status"));
        Assert.Equal(
            readiness.RootElement.GetProperty("status").GetString(),
            summarizedReadiness.RootElement
                .GetProperty("readiness")
                .GetProperty("status")
                .GetString());
        if (readiness.RootElement.GetProperty("status").GetString() != "Healthy")
            Assert.NotEqual(
                "Healthy",
                summarizedReadiness.RootElement.GetProperty("status").GetString());

        var current = await ReadJsonAsync(await _client.GetAsync("/api/operations/status"));
        var safeMode = current.RootElement.GetProperty("safeMode");
        var revision = safeMode.GetProperty("revision").GetInt64();
        var antiforgery = await ReadJsonAsync(await _client.GetAsync("/api/auth/antiforgery"));
        var token = antiforgery.RootElement.GetProperty("token").GetString();
        var mutationBefore = Baseline("SafeMode.Activated");
        using var activate = new HttpRequestMessage(HttpMethod.Post, "/api/operations/safe-mode")
        {
            Content = JsonContent.Create(new
            {
                enabled = true,
                expectedRevision = revision,
                reason = "Operations acceptance test",
                confirmation = "ACTIVATE SAFE MODE"
            })
        };
        activate.Headers.Add("X-XSRF-TOKEN", token);
        var activatedResponse = await _client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.OK, activatedResponse.StatusCode);
        var activated = await ReadJsonAsync(activatedResponse);
        Assert.Equal(mutationBefore + 1, Baseline("SafeMode.Activated"));

        Guid environmentId;
        long validationAuditBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            environmentId = db.RuntimeEnvironments.AsNoTracking()
                .Where(item => item.WorkspaceId == WorkspaceIdentityDefaults.WorkspaceId
                               && item.IsDefault
                               && item.Status == "Active")
                .Select(item => item.Id)
                .Single();
            validationAuditBefore = db.ConfigurationChanges.LongCount(
                item => item.SettingKey == "ai.provider_validation");
        }
        var blockedToken = await ReadJsonAsync(await _client.GetAsync("/api/auth/antiforgery"));
        using var blockedValidation = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/environments/{environmentId}/settings/provider/validate");
        blockedValidation.Headers.Add(
            "X-XSRF-TOKEN",
            blockedToken.RootElement.GetProperty("token").GetString());
        var blockedResponse = await _client.SendAsync(blockedValidation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blockedResponse.StatusCode);
        var blockedProblem = await ReadJsonAsync(blockedResponse);
        Assert.Equal(
            "operations.safe_mode_active",
            blockedProblem.RootElement.GetProperty("code").GetString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(
                validationAuditBefore + 1,
                db.ConfigurationChanges.LongCount(
                    item => item.SettingKey == "ai.provider_validation"));
        }

        var deactivateToken = await ReadJsonAsync(await _client.GetAsync("/api/auth/antiforgery"));
        using var deactivate = new HttpRequestMessage(HttpMethod.Post, "/api/operations/safe-mode")
        {
            Content = JsonContent.Create(new
            {
                enabled = false,
                expectedRevision = activated.RootElement.GetProperty("revision").GetInt64(),
                reason = "Operations acceptance test complete",
                confirmation = "DEACTIVATE SAFE MODE"
            })
        };
        deactivate.Headers.Add("X-XSRF-TOKEN", deactivateToken.RootElement.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(deactivate)).StatusCode);
    }

    [Fact]
    public async Task Operations_endpoints_reject_every_non_platform_workspace_role()
    {
        foreach (var role in new[] { "Administrator", "Engineer", "Reviewer", "Operator", "Viewer" })
        {
            var userId = Guid.NewGuid();
            var email = $"{role.ToLowerInvariant()}-{userId:N}@convolab.test";
            const string password = "Operations-Role-Test-42!";
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTimeOffset.UtcNow;
                var user = new IdentityUserRecord
                {
                    Id = userId,
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    DisplayName = $"Operations {role}",
                    Status = "Active",
                    IsPlatformAdministrator = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.IdentityUsers.Add(user);
                db.WorkspaceMemberships.Add(new WorkspaceMembershipRecord
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId,
                    UserId = userId,
                    Role = role,
                    Status = "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                db.LocalCredentials.Add(new LocalCredentialRecord
                {
                    UserId = userId,
                    PasswordHash = new PasswordHasher<IdentityUserRecord>()
                        .HashPassword(user, password),
                    UpdatedAt = now
                });
                await db.SaveChangesAsync();
            }

            using var client = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password })).StatusCode);
            foreach (var route in new[]
            {
                "/api/operations/status",
                "/api/operations/readiness",
                "/api/operations/workers",
                "/api/operations/analytics-pipeline",
                "/api/operations/authentication",
                "/api/operations/secret-providers",
                "/api/operations/backups",
                "/api/operations/build",
                "/api/operations/telemetry"
            })
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
        }
    }

    [Fact]
    public async Task Analytics_field_visibility_blocks_production_cost_event_and_correlation_bypasses()
    {
        var environmentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var correlationId = $"security-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RuntimeEnvironments.Add(new RuntimeEnvironmentRecord
            {
                Id = environmentId,
                OrganisationId = WorkspaceIdentityDefaults.OrganisationId,
                WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId,
                Name = $"Production security {environmentId:N}",
                Slug = $"production-security-{environmentId:N}",
                EnvironmentType = "Production",
                Description = "Analytics field-visibility test environment.",
                Status = "Active",
                IsDefault = false,
                CreatedAt = now,
                CreatedBy = WorkspaceIdentityDefaults.BootstrapUserId,
                UpdatedAt = now,
                Revision = 1
            });
            db.AnalyticsEvents.Add(new AnalyticsEventRecord
            {
                Id = eventId,
                EventKey = $"security-{eventId:N}",
                OrganisationId = WorkspaceIdentityDefaults.OrganisationId,
                WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId,
                EnvironmentId = environmentId,
                ActorId = actorId,
                ActorType = "User",
                ActorRole = "Administrator",
                Capability = "Provider",
                EventType = "ProviderInvocationCompleted",
                Outcome = "Succeeded",
                Provider = "Restricted provider",
                Model = "restricted-model",
                InputTokens = 321,
                OutputTokens = 123,
                CostZar = 9.876543m,
                CostType = "Actual",
                PricingRevision = "restricted-pricing",
                DurationMs = 42,
                ProviderInvocationPrevented = false,
                SourceExecutionId = sourceId,
                SourceType = "SimulationRun",
                SourceId = sourceId,
                PromptName = "Restricted prompt",
                WorkflowName = "Restricted workflow",
                ConfigurationRevision = "restricted-configuration",
                CorrelationId = correlationId,
                OccurredAt = now
            });
            await db.SaveChangesAsync();
        }

        using var engineer = await CreateRoleClientAsync(WorkspaceRole.Engineer);
        var engineerCost = await engineer.GetAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/cost?environmentId={environmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, engineerCost.StatusCode);
        await AssertProtectedAnalyticsFieldsAsync(
            await engineer.GetAsync(
                $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/events?environmentId={environmentId}&eventType=ProviderInvocationCompleted"),
            paged: true);
        await AssertProtectedAnalyticsFieldsAsync(
            await engineer.GetAsync(
                $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/correlations/{correlationId}"),
            paged: false);

        using var reviewer = await CreateRoleClientAsync(WorkspaceRole.Reviewer);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await reviewer.GetAsync(
                $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/cost?environmentId={environmentId}")).StatusCode);
        await AssertProtectedAnalyticsFieldsAsync(
            await reviewer.GetAsync(
                $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/events?environmentId={environmentId}&eventType=ProviderInvocationCompleted"),
            paged: true);

        using var viewer = await CreateRoleClientAsync(WorkspaceRole.Viewer);
        Assert.Equal(
            HttpStatusCode.OK,
            (await viewer.GetAsync(
                $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/overview?environmentId={environmentId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await viewer.GetAsync(
                $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/events?environmentId={environmentId}")).StatusCode);

        var administratorResponse = await _client.GetAsync(
            $"/api/workspaces/{WorkspaceIdentityDefaults.WorkspaceId}/analytics/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, administratorResponse.StatusCode);
        var administratorEvent = await ReadJsonAsync(administratorResponse);
        Assert.Equal(actorId, administratorEvent.RootElement.GetProperty("actorId").GetGuid());
        Assert.Equal(321, administratorEvent.RootElement.GetProperty("inputTokens").GetInt32());
        Assert.Equal(9.876543m, administratorEvent.RootElement.GetProperty("costZar").GetDecimal());
        Assert.Equal(sourceId, administratorEvent.RootElement.GetProperty("sourceId").GetGuid());
    }

    private async Task<HttpClient> CreateRoleClientAsync(WorkspaceRole role)
    {
        var userId = Guid.NewGuid();
        var token = $"role-test-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.IdentityUsers.Add(new IdentityUserRecord
            {
                Id = userId,
                Email = $"{role.ToString().ToLowerInvariant()}-{userId:N}@convolab.test",
                NormalizedEmail = $"{role.ToString().ToUpperInvariant()}-{userId:N}@CONVOLAB.TEST",
                DisplayName = $"{role} analytics test",
                Status = "Active",
                IsPlatformAdministrator = false,
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.WorkspaceMemberships.Add(new WorkspaceMembershipRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId,
                UserId = userId,
                Role = role.ToString(),
                Status = "Active",
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.AuthenticationSessions.Add(new AuthenticationSessionRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ActiveWorkspaceId = WorkspaceIdentityDefaults.WorkspaceId,
                TokenHash = ConvoLabAuthentication.HashSecret(token),
                CreatedAt = now,
                LastSeenAt = now,
                ExpiresAt = now.AddMinutes(15)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{ConvoLabAuthentication.SessionCookie}={token}");
        return client;
    }

    private static async Task AssertProtectedAnalyticsFieldsAsync(HttpResponseMessage response, bool paged)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected analytics payload, received {(int)response.StatusCode}: {payload}");
        var document = JsonDocument.Parse(payload);
        var events = paged
            ? document.RootElement.GetProperty("items")
            : document.RootElement;
        var analyticsEvent = Assert.Single(events.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, analyticsEvent.GetProperty("actorId").ValueKind);
        Assert.Equal(JsonValueKind.Null, analyticsEvent.GetProperty("inputTokens").ValueKind);
        Assert.Equal(JsonValueKind.Null, analyticsEvent.GetProperty("outputTokens").ValueKind);
        Assert.Equal(JsonValueKind.Null, analyticsEvent.GetProperty("costZar").ValueKind);
        Assert.Equal("Restricted", analyticsEvent.GetProperty("costType").GetString());
        Assert.Equal(JsonValueKind.Null, analyticsEvent.GetProperty("sourceId").ValueKind);
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
                ["Knowledge:StoragePath"] = Path.Combine(Path.GetTempPath(), "convolab-api-tests"),
                ["Bootstrap:Administrator:Email"] = "admin@convolab.test",
                ["Bootstrap:Administrator:DisplayName"] = "Test Administrator",
                ["Bootstrap:Administrator:Password"] = "Ephemeral-Alpha12!"
            });
        });
    }
}
