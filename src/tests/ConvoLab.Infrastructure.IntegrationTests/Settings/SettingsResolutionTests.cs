using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConvoLab.Infrastructure.IntegrationTests.Settings;

/// <summary>
/// Integration tests for the settings stack against a real SQLite database with
/// the full migration set applied: environment lifecycle, effective configuration
/// resolution across scopes, typed validation on write, Production safeguards,
/// audit trail, validation report, and export.
/// </summary>
public sealed class SettingsResolutionTests : IAsyncLifetime
{
    private ApplicationDbContext _db = null!;
    private SettingsService _settings = null!;
    private EnvironmentService _environments = null!;
    private EffectiveConfigurationResolver _resolver = null!;

    private static readonly Guid OrganisationId = Guid.Parse("10000000-0000-0000-0000-0000000000AA");
    private static readonly Guid WorkspaceId = Guid.Parse("20000000-0000-0000-0000-0000000000BB");
    private static readonly Guid ActorId = Guid.Parse("30000000-0000-0000-0000-0000000000CC");
    private const string Actor = "Integration Tester";
    private const string Correlation = "itest-corr";

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new ApplicationDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();

        _db.Organisations.Add(new OrganisationRecord
        {
            Id = OrganisationId,
            Name = "Integration Organisation",
            Slug = "integration-organisation",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _db.Workspaces.Add(new WorkspaceRecord
        {
            Id = WorkspaceId,
            OrganisationId = OrganisationId,
            Name = "Integration Workspace",
            Slug = "integration-workspace",
            Description = "Settings integration tests",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().Build();
        _resolver = new EffectiveConfigurationResolver(
            _db, configuration, NullLogger<EffectiveConfigurationResolver>.Instance);
        _settings = new SettingsService(_db, _resolver);
        _environments = new EnvironmentService(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private Task<EnvironmentDto> CreateEnvironmentAsync(string name, string type = "Development") =>
        _environments.CreateAsync(WorkspaceId,
            new CreateEnvironmentRequest(name, name.ToLowerInvariant().Replace(' ', '-'), type, $"{name} environment", false),
            ActorId, Actor, Correlation);

    // ─── Migration backfill ──────────────────────────────────────────────────

    [Fact]
    public async Task Migration_seeds_setting_definitions()
    {
        var count = await _db.SettingDefinitions.CountAsync();
        Assert.True(count >= 40, $"Expected at least 40 seeded definitions, found {count}.");
    }

    // ─── Environment lifecycle round-trip through the service ────────────────

    [Fact]
    public async Task Environment_create_and_get_round_trips()
    {
        var created = await CreateEnvironmentAsync("Integration Env");

        var fetched = await _environments.GetAsync(WorkspaceId, created.Id);
        Assert.Equal("Integration Env", fetched.Name);
        Assert.Equal("Active", fetched.Status);
    }

    // ─── Effective resolution across scopes ──────────────────────────────────

    [Fact]
    public async Task Effective_configuration_resolves_platform_default_then_environment_override()
    {
        var env = await CreateEnvironmentAsync("Resolution Env");

        // Platform default applies before any override.
        var effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        var temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.Equal("Platform", temperature.SourceScope);
        Assert.True(temperature.IsInherited);

        // Environment override wins after upsert.
        await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            new UpsertSettingRequest("0.55", "integration test", null), ActorId, Actor, Correlation);

        effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.Equal("Environment", temperature.SourceScope);
        Assert.False(temperature.IsInherited);
        Assert.Contains("0.55", temperature.EffectiveValue);
    }

    [Fact]
    public async Task Workspace_override_applies_but_environment_override_wins()
    {
        var env = await CreateEnvironmentAsync("Precedence Env");

        await _settings.UpsertWorkspaceSettingAsync(WorkspaceId, "ai.temperature",
            new UpsertSettingRequest("0.4", "workspace level", null), ActorId, Actor, Correlation);

        var effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        var temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.Equal("Workspace", temperature.SourceScope);

        await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            new UpsertSettingRequest("0.9", "environment beats workspace", null), ActorId, Actor, Correlation);

        effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.Equal("Environment", temperature.SourceScope);
        Assert.Contains("0.9", temperature.EffectiveValue);
    }

    [Fact]
    public async Task Deleting_override_restores_inherited_value()
    {
        var env = await CreateEnvironmentAsync("Delete Env");

        await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            new UpsertSettingRequest("0.9", "temp override", null), ActorId, Actor, Correlation);
        await _settings.DeleteEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            ActorId, Actor, Correlation);

        var effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        var temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.True(temperature.IsInherited);
    }

    [Fact]
    public async Task Reupserting_same_key_updates_row_instead_of_duplicating()
    {
        var env = await CreateEnvironmentAsync("Dedup Env");

        var first = await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            new UpsertSettingRequest("0.5", "first", null), ActorId, Actor, Correlation);
        var second = await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            new UpsertSettingRequest("0.6", "second", null), ActorId, Actor, Correlation);

        Assert.Equal(first.Id, second.Id);
        Assert.True(second.Revision > first.Revision);
    }

    // ─── Typed validation on write ───────────────────────────────────────────

    [Fact]
    public async Task Upsert_rejects_out_of_range_value()
    {
        var env = await CreateEnvironmentAsync("Validation Env");

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
                new UpsertSettingRequest("9.9", "invalid", null), ActorId, Actor, Correlation));

        Assert.Contains("at most", ex.Message);
    }

    [Fact]
    public async Task Upsert_rejects_credential_shaped_string_values()
    {
        var env = await CreateEnvironmentAsync("Leak Env");

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.model",
                new UpsertSettingRequest("sk-abcdefghijklmnop1234567890", "leak", null),
                ActorId, Actor, Correlation));
    }

    // ─── Production safeguards ───────────────────────────────────────────────

    [Fact]
    public async Task Production_change_requires_reason()
    {
        var prod = await CreateEnvironmentAsync("Prod Guard", "Production");

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _settings.UpsertEnvironmentSettingAsync(WorkspaceId, prod.Id, "ai.temperature",
                new UpsertSettingRequest("0.3", null, null), ActorId, Actor, Correlation));

        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disabling_enforcement_in_production_requires_explicit_confirmation()
    {
        var prod = await CreateEnvironmentAsync("Prod Enforce", "Production");

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _settings.UpsertEnvironmentSettingAsync(WorkspaceId, prod.Id, "policy.enforcement_enabled",
                new UpsertSettingRequest("false", "drill", null), ActorId, Actor, Correlation));

        // With explicit confirmation the change is allowed and audited.
        var result = await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, prod.Id, "policy.enforcement_enabled",
            new UpsertSettingRequest("false", "authorised drill", null, ConfirmProtectedChange: true),
            ActorId, Actor, Correlation);

        Assert.Contains("false", result.ValueJson);
    }

    // ─── Audit trail ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Changes_are_audited_with_actor_and_reason()
    {
        var env = await CreateEnvironmentAsync("Audit Env");

        await _settings.UpsertEnvironmentSettingAsync(WorkspaceId, env.Id, "ai.temperature",
            new UpsertSettingRequest("0.8", "audit check", null), ActorId, Actor, Correlation);

        var history = await _settings.GetChangeHistoryAsync(WorkspaceId, env.Id);
        var entry = history.First(c => c.SettingKey == "ai.temperature");

        Assert.Equal(Actor, entry.ChangedByDisplay);
        Assert.Equal("audit check", entry.Reason);
        Assert.Equal("Succeeded", entry.Outcome);
    }

    // ─── Validation report + export ──────────────────────────────────────────

    [Fact]
    public async Task Validate_environment_settings_reports_all_definitions()
    {
        var env = await CreateEnvironmentAsync("Validate Env");

        var report = await _settings.ValidateEnvironmentSettingsAsync(WorkspaceId, env.Id);

        Assert.True(report.IsValid);
        Assert.True(report.CheckedCount >= 40);
        Assert.Equal(0, report.InvalidCount);
    }

    [Fact]
    public async Task Export_produces_versioned_snapshot_without_secret_material()
    {
        var env = await CreateEnvironmentAsync("Export Env");

        var export = await _settings.ExportAsync(WorkspaceId, env.Id);

        Assert.NotEmpty(export.Settings);
        Assert.All(export.Settings, s =>
            Assert.DoesNotContain("sk-", s.Value ?? "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_validate_only_previews_without_applying()
    {
        var env = await CreateEnvironmentAsync("Import Env");

        var settingsJson =
            "{\"schemaVersion\":\"1.0\",\"organisation\":\"Integration Organisation\"," +
            "\"workspace\":\"Integration Workspace\",\"environment\":\"Import Env\"," +
            $"\"exportedAt\":\"{DateTimeOffset.UtcNow:O}\"," +
            "\"settings\":[{\"key\":\"ai.temperature\",\"category\":\"AI Provider\"," +
            "\"displayName\":\"Temperature\",\"value\":\"0.65\"}],\"featureFlags\":[]}";
        await _settings.ImportAsync(WorkspaceId, env.Id,
            new ImportConfigurationRequest(settingsJson, ValidateOnly: true, "preview"),
            ActorId, Actor, Correlation);

        var effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        var temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.True(temperature.IsInherited);

        await _settings.ImportAsync(WorkspaceId, env.Id,
            new ImportConfigurationRequest(settingsJson, ValidateOnly: false, "apply"),
            ActorId, Actor, Correlation);

        effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, env.Id);
        temperature = effective.Single(s => s.Key == "ai.temperature");
        Assert.False(temperature.IsInherited);
        Assert.Contains("0.65", temperature.EffectiveValue);
    }

    [Fact]
    public async Task Environment_operations_reject_an_environment_owned_by_another_workspace()
    {
        var foreignOrganisationId = Guid.NewGuid();
        var foreignWorkspaceId = Guid.NewGuid();
        var foreignEnvironmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _db.Organisations.Add(new OrganisationRecord
        {
            Id = foreignOrganisationId,
            Name = "Foreign Organisation",
            Slug = $"foreign-{foreignOrganisationId:N}",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.Workspaces.Add(new WorkspaceRecord
        {
            Id = foreignWorkspaceId,
            OrganisationId = foreignOrganisationId,
            Name = "Foreign Workspace",
            Slug = $"foreign-{foreignWorkspaceId:N}",
            Description = "Isolation test",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.RuntimeEnvironments.Add(new RuntimeEnvironmentRecord
        {
            Id = foreignEnvironmentId,
            OrganisationId = foreignOrganisationId,
            WorkspaceId = foreignWorkspaceId,
            Name = "Foreign Environment",
            Slug = $"foreign-{foreignEnvironmentId:N}",
            EnvironmentType = "Development",
            Description = "Isolation test",
            Status = "Active",
            IsDefault = true,
            CreatedAt = now,
            CreatedBy = ActorId,
            UpdatedAt = now,
            Revision = 1
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _settings.ListEnvironmentSettingsAsync(WorkspaceId, foreignEnvironmentId));
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, foreignEnvironmentId));
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _settings.DeleteEnvironmentSettingAsync(
                WorkspaceId, foreignEnvironmentId, SettingKeys.AiTemperature,
                ActorId, Actor, Correlation));
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _resolver.CreateSnapshotAsync(OrganisationId, WorkspaceId, foreignEnvironmentId));
    }

    [Fact]
    public async Task Configuration_snapshot_revision_is_stable_and_content_addressed()
    {
        var environment = await CreateEnvironmentAsync("Revision Env");

        var first = await _resolver.CreateSnapshotAsync(OrganisationId, WorkspaceId, environment.Id);
        var second = await _resolver.CreateSnapshotAsync(OrganisationId, WorkspaceId, environment.Id);

        Assert.StartsWith("sha256:", first.ConfigurationRevision);
        Assert.Equal(first.ConfigurationRevision, second.ConfigurationRevision);

        await _settings.UpsertEnvironmentSettingAsync(
            WorkspaceId, environment.Id, SettingKeys.AiTemperature,
            new UpsertSettingRequest("0.61", "revision test", null),
            ActorId, Actor, Correlation);

        var changed = await _resolver.CreateSnapshotAsync(OrganisationId, WorkspaceId, environment.Id);
        Assert.NotEqual(first.ConfigurationRevision, changed.ConfigurationRevision);
    }

    [Fact]
    public async Task Effective_numeric_values_use_json_number_representation()
    {
        var environment = await CreateEnvironmentAsync("Typed Values Env");
        await _settings.UpsertEnvironmentSettingAsync(
            WorkspaceId, environment.Id, SettingKeys.AiTemperature,
            new UpsertSettingRequest("\"0.55\"", "typed value test", null),
            ActorId, Actor, Correlation);

        var effective = await _settings.GetEffectiveEnvironmentSettingsAsync(WorkspaceId, environment.Id);
        Assert.Equal("0.55", effective.Single(value => value.Key == SettingKeys.AiTemperature).EffectiveValue);
        var budget = effective.Single(value => value.Key == SettingKeys.MonthlyBudgetZar).EffectiveValue;
        Assert.NotNull(budget);
        Assert.DoesNotContain('"', budget);
    }
}
