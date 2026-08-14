using ConvoLab.Domain.WorkspaceIdentity;

namespace ConvoLab.Domain.Tests.WorkspaceIdentity;

public sealed class WorkspaceIdentityTests
{
    [Fact]
    public void Archived_workspace_is_immutable()
    {
        var workspace = new Workspace(Guid.NewGuid(), Guid.NewGuid(), "Claims", "claims", "Claims team");
        workspace.Archive();
        Assert.Throws<InvalidOperationException>(() => workspace.Update("Renamed", "Changed"));
    }

    [Fact]
    public void Final_administrator_cannot_be_removed()
    {
        var membership = new WorkspaceMembership(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkspaceRole.Administrator, MembershipStatus.Active);
        Assert.Throws<InvalidOperationException>(() => membership.Remove(finalAdministrator: true));
    }

    [Fact]
    public void Engineer_permissions_do_not_include_member_management()
    {
        var permissions = WorkspacePermissions.For(WorkspaceRole.Engineer);
        Assert.Contains(WorkspacePermissions.RunSimulation, permissions);
        Assert.Contains(WorkspacePermissions.InspectSensitiveTrace, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ManageMembers, permissions);
        Assert.Contains(WorkspacePermissions.ViewWorkspaceAnalytics, permissions);
        Assert.Contains(WorkspacePermissions.ViewCostAnalytics, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ViewActorAnalytics, permissions);
    }

    [Fact]
    public void Viewer_is_non_sensitive_read_only()
    {
        // Viewers may read configuration and aggregated analytics, never sensitive detail.
        var permissions = WorkspacePermissions.For(WorkspaceRole.Viewer);

        Assert.Equal(3, permissions.Count);
        Assert.Contains(WorkspacePermissions.WorkspaceMember, permissions);
        Assert.Contains(WorkspacePermissions.ViewSettings, permissions);
        Assert.Contains(WorkspacePermissions.ViewWorkspaceAnalytics, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ViewCostAnalytics, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ExportAnalytics, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ManageWorkspaceSettings, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ManageEnvironmentSettings, permissions);
        Assert.DoesNotContain(WorkspacePermissions.ManageSecretReferences, permissions);
    }

    [Fact]
    public void External_identity_requires_stable_provider_issuer_and_subject()
    {
        Assert.Throws<ArgumentException>(() => new ExternalIdentity(
            Guid.NewGuid(), Guid.NewGuid(), "Entra", "", "subject"));
        var identity = new ExternalIdentity(Guid.NewGuid(), Guid.NewGuid(), "Entra",
            "https://issuer.example/v2.0", "stable-subject");
        identity.Disable();
        Assert.False(identity.IsActive);
        Assert.Equal(2, identity.Revision);
        identity.Enable();
        Assert.True(identity.IsActive);
        Assert.Equal(3, identity.Revision);
    }

    [Fact]
    public void Authentication_providers_are_fixed_platform_values()
    {
        Assert.Equal(["Local", "Entra", "ServiceAccount", "BreakGlass"],
            Enum.GetNames<AuthenticationProvider>());
    }
}
