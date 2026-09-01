using ConvoLab.Application.Operations;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.IntegrationTests.Settings;

public sealed class RequiredSecretReadinessTests
{
    [Fact]
    public async Task Bootstrapper_defaults_development_environment_to_deterministic_when_no_secret_is_present()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();

        var organisationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Organisations.Add(new OrganisationRecord
        {
            Id = organisationId,
            Name = "Bootstrap workspace",
            Slug = $"bootstrap-{organisationId:N}",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Workspaces.Add(new WorkspaceRecord
        {
            Id = workspaceId,
            OrganisationId = organisationId,
            Name = "Bootstrap workspace",
            Slug = $"bootstrap-{workspaceId:N}",
            Description = "Default environment bootstrap",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var bootstrapper = new SettingsBootstrapper(
            db,
            new ConfigurationBuilder().Build(),
            NullLogger<SettingsBootstrapper>.Instance);
        await bootstrapper.ApplyAsync();

        var environment = await db.RuntimeEnvironments
            .SingleAsync(item => item.WorkspaceId == workspaceId && item.IsDefault && item.EnvironmentType == "Development");
        var provider = await db.SettingValues
            .Where(item => item.WorkspaceId == workspaceId && item.EnvironmentId == environment.Id && item.DefinitionKey == SettingKeys.AiProvider)
            .Select(item => item.ValueJson)
            .SingleAsync();
        var secretReference = await db.SettingValues
            .Where(item => item.WorkspaceId == workspaceId && item.EnvironmentId == environment.Id && item.DefinitionKey == SettingKeys.AiSecretReference)
            .Select(item => item.ValueJson)
            .SingleAsync();

        Assert.Equal("\"Deterministic\"", provider);
        Assert.Equal("\"\"", secretReference);
    }

    [Fact]
    public async Task Readiness_validates_only_effective_required_active_environment_secrets()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var organisationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Organisations.Add(new OrganisationRecord
        {
            Id = organisationId,
            Name = "Readiness organisation",
            Slug = $"readiness-{organisationId:N}",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Workspaces.Add(new WorkspaceRecord
        {
            Id = workspaceId,
            OrganisationId = organisationId,
            Name = "Readiness workspace",
            Slug = $"readiness-{workspaceId:N}",
            Description = "Required-secret evidence",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.RuntimeEnvironments.AddRange(
            Environment(requiredId, organisationId, workspaceId, "Required Production", "Active"),
            Environment(disabledId, organisationId, workspaceId, "Disabled Production", "Active"),
            Environment(inactiveId, organisationId, workspaceId, "Archived Production", "Archived"));
        db.SettingValues.AddRange(
            Setting(workspaceId, null, "Workspace", "ai.secret_reference", "\"env:STALE_RAW_REFERENCE\""),
            Setting(workspaceId, requiredId, "Environment", "ai.provider", "\"Gemini\""),
            Setting(workspaceId, requiredId, "Environment", "ai.provider_enabled", "true"),
            Setting(workspaceId, requiredId, "Environment", "feature.provider_execution", "true"),
            Setting(workspaceId, requiredId, "Environment", "ai.secret_reference", "\"env:EFFECTIVE_REFERENCE\""),
            Setting(workspaceId, disabledId, "Environment", "ai.provider", "\"Gemini\""),
            Setting(workspaceId, disabledId, "Environment", "ai.provider_enabled", "false"),
            Setting(workspaceId, disabledId, "Environment", "ai.secret_reference", "\"env:DISABLED_REFERENCE\""),
            Setting(workspaceId, inactiveId, "Environment", "ai.provider", "\"Gemini\""),
            Setting(workspaceId, inactiveId, "Environment", "ai.secret_reference", "\"env:ARCHIVED_REFERENCE\""));
        await db.SaveChangesAsync();

        var resolver = new EffectiveConfigurationResolver(
            db,
            new ConfigurationBuilder().Build(),
            NullLogger<EffectiveConfigurationResolver>.Instance);
        var secrets = new CapturingSecretStore();
        var evaluator = new RequiredSecretReadinessEvaluator(
            db,
            resolver,
            secrets,
            new TestEnvironment("Production"),
            Options.Create(new RequiredSecretReadinessOptions
            {
                ProductionEnvironmentIdsOrNames = ["Required Production", "Disabled Production"]
            }));

        var snapshot = await evaluator.EvaluateAsync();

        Assert.Empty(snapshot.ScopeFailureCodes);
        Assert.Equal(2, snapshot.Environments.Count);
        Assert.Collection(
            secrets.ValidatedReferences,
            value => Assert.Equal("env:EFFECTIVE_REFERENCE", value));
        var required = Assert.Single(snapshot.Environments, item => item.Required);
        Assert.Equal(OperationalDependencyState.StubValidated, required.DependencyState);
        Assert.Equal("env", required.SecretProviderScheme);
        Assert.Contains(snapshot.Environments, item =>
            item.EnvironmentId == disabledId
            && !item.Required
            && item.DependencyState == OperationalDependencyState.Configured);
        var serialized = System.Text.Json.JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("EFFECTIVE_REFERENCE", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("STALE_RAW_REFERENCE", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("DISABLED_REFERENCE", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("ARCHIVED_REFERENCE", serialized, StringComparison.Ordinal);
    }

    private static RuntimeEnvironmentRecord Environment(
        Guid id,
        Guid organisationId,
        Guid workspaceId,
        string name,
        string status) => new()
    {
        Id = id,
        OrganisationId = organisationId,
        WorkspaceId = workspaceId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        EnvironmentType = "Production",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = Guid.NewGuid(),
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static SettingValueRecord Setting(
        Guid workspaceId,
        Guid? environmentId,
        string scope,
        string key,
        string value) => new()
    {
        Id = Guid.NewGuid(),
        DefinitionKey = key,
        Scope = scope,
        WorkspaceId = scope == "Workspace" ? workspaceId : null,
        EnvironmentId = environmentId,
        ValueJson = value,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        CreatedBy = Guid.NewGuid(),
        UpdatedBy = Guid.NewGuid()
    };

    private sealed class CapturingSecretStore : ISecretStore
    {
        public List<string> ValidatedReferences { get; } = [];
        public Task<SecretResolutionResult> ResolveAsync(string reference, CancellationToken ct = default) =>
            Task.FromResult(SecretResolutionResult.Resolved("env", "never-exposed"));
        public Task<SecretValidationResult> ValidateAsync(string reference, CancellationToken ct = default)
        {
            ValidatedReferences.Add(reference);
            return Task.FromResult(new SecretValidationResult(
                "env",
                SecretResolutionStatus.Resolved,
                null,
                OperationalDependencyState.StubValidated));
        }
        public void Invalidate(string reference) { }
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ConvoLab.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
