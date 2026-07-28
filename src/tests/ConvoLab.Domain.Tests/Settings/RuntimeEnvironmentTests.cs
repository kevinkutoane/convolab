using ConvoLab.Domain.Settings;

namespace ConvoLab.Domain.Tests.Settings;

/// <summary>
/// Tests for the RuntimeEnvironment lifecycle and SecretReference invariants that
/// underpin environment isolation and safe secret handling.
/// </summary>
public sealed class RuntimeEnvironmentTests
{
    private static readonly Guid OrganisationId = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static RuntimeEnvironment CreateEnvironment(
        EnvironmentType type = EnvironmentType.Development,
        bool isDefault = false) =>
        new(Guid.NewGuid(), OrganisationId, WorkspaceId,
            "Test Environment", "test-environment", type, "Test environment",
            isDefault, ActorId, DateTimeOffset.UtcNow);

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    [Fact]
    public void New_environment_starts_active()
    {
        var env = CreateEnvironment();
        Assert.Equal(EnvironmentStatus.Active, env.Status);
    }

    [Fact]
    public void Slug_is_normalised_to_lowercase()
    {
        var env = new RuntimeEnvironment(
            Guid.NewGuid(), OrganisationId, WorkspaceId,
            "Staging", "STAGING", EnvironmentType.Staging, "",
            false, ActorId, DateTimeOffset.UtcNow);

        Assert.Equal("staging", env.Slug);
    }

    [Fact]
    public void Suspend_then_activate_round_trips()
    {
        var env = CreateEnvironment();

        env.Suspend(isLastActiveProduction: false, isAdmin: false);
        Assert.Equal(EnvironmentStatus.Suspended, env.Status);

        env.Activate(ActorId);
        Assert.Equal(EnvironmentStatus.Active, env.Status);
    }

    [Fact]
    public void Last_active_production_cannot_be_suspended_by_non_admin()
    {
        var env = CreateEnvironment(EnvironmentType.Production);
        Assert.Throws<InvalidOperationException>(
            () => env.Suspend(isLastActiveProduction: true, isAdmin: false));
    }

    [Fact]
    public void Last_active_production_can_be_suspended_by_admin()
    {
        var env = CreateEnvironment(EnvironmentType.Production);
        env.Suspend(isLastActiveProduction: true, isAdmin: true);
        Assert.Equal(EnvironmentStatus.Suspended, env.Status);
    }

    [Fact]
    public void Archived_environment_cannot_be_reactivated_or_mutated()
    {
        var env = CreateEnvironment();
        env.Archive();

        Assert.Equal(EnvironmentStatus.Archived, env.Status);
        Assert.Throws<InvalidOperationException>(() => env.Activate(ActorId));
        Assert.Throws<InvalidOperationException>(
            () => env.Update("New Name", "desc", EnvironmentType.Development, ActorId));
    }

    [Fact]
    public void Default_environment_cannot_be_archived()
    {
        var env = CreateEnvironment(isDefault: true);
        Assert.Throws<InvalidOperationException>(env.Archive);
    }

    [Fact]
    public void Only_active_environments_can_become_default()
    {
        var env = CreateEnvironment();
        env.Suspend(isLastActiveProduction: false, isAdmin: false);
        Assert.Throws<InvalidOperationException>(() => env.MakeDefault(ActorId));
    }

    [Fact]
    public void Update_requires_non_empty_name()
    {
        var env = CreateEnvironment();
        Assert.Throws<ArgumentException>(
            () => env.Update(" ", "desc", EnvironmentType.Development, ActorId));
    }

    [Fact]
    public void Mutations_increment_revision()
    {
        var env = CreateEnvironment();
        var initial = env.Revision;

        env.Update("Renamed", "desc", EnvironmentType.Staging, ActorId);
        Assert.Equal(initial + 1, env.Revision);
    }

    // ─── SecretReference ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("env:GEMINI_API_KEY", "env", "GEMINI_API_KEY")]
    [InlineData("vault:kv/data/ai#api_key", "vault", "kv/data/ai#api_key")]
    [InlineData("aws:convolab/prod/gemini", "aws", "convolab/prod/gemini")]
    public void ParseReference_extracts_provider_and_key(string reference, string provider, string key)
    {
        var (parsedProvider, parsedKey) = SecretReference.ParseReference(reference);
        Assert.Equal(provider, parsedProvider);
        Assert.Equal(key, parsedKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-colon-here")]
    [InlineData(":missing-provider")]
    public void ParseReference_rejects_malformed_references(string reference) =>
        Assert.Throws<ArgumentException>(() => SecretReference.ParseReference(reference));

    [Fact]
    public void SecretReference_never_stores_material_and_starts_unvalidated()
    {
        var secret = new SecretReference(
            Guid.NewGuid(), WorkspaceId, "Gemini Key", "env:GEMINI_API_KEY",
            ActorId, DateTimeOffset.UtcNow);

        Assert.Equal(SecretReferenceStatus.NotValidated, secret.Status);
        Assert.Equal("env", secret.Provider);
        Assert.DoesNotContain("sk-", secret.Reference);
    }

    [Fact]
    public void RecordValidation_transitions_status()
    {
        var secret = new SecretReference(
            Guid.NewGuid(), WorkspaceId, "Gemini Key", "env:GEMINI_API_KEY",
            ActorId, DateTimeOffset.UtcNow);

        secret.RecordValidation(true, "Resolved.", ActorId);
        Assert.Equal(SecretReferenceStatus.Valid, secret.Status);

        secret.MarkMissing();
        Assert.Equal(SecretReferenceStatus.Missing, secret.Status);
    }
}
