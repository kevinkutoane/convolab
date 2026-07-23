namespace ConvoLab.Domain.WorkspaceIdentity;

public enum OrganisationStatus { Active, Suspended, Archived }
public enum WorkspaceStatus { Active, Suspended, Archived }
public enum IdentityUserStatus { Invited, Active, Suspended, Disabled }
public enum MembershipStatus { Invited, Active, Suspended, Removed }
public enum WorkspaceRole { Administrator, Engineer, Reviewer, Operator, Viewer }
public enum ServiceAccountStatus { Active, Revoked, Expired }
public enum AuditScope { Platform, Organisation, Workspace }

public static class WorkspaceIdentityDefaults
{
    public static readonly Guid OrganisationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid WorkspaceId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid BootstrapUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid BootstrapMembershipId = Guid.Parse("40000000-0000-0000-0000-000000000001");
}

public sealed class Organisation
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string Slug { get; }
    public OrganisationStatus Status { get; private set; }
    public long Revision { get; private set; }

    public Organisation(Guid id, string name, string slug, OrganisationStatus status = OrganisationStatus.Active, long revision = 1)
    {
        if (id == Guid.Empty) throw new ArgumentException("Organisation id is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organisation name is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Organisation slug is required.");
        Id = id; Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant(); Status = status; Revision = revision;
    }

    public void Update(string name) { EnsureMutable(); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organisation name is required."); Name = name.Trim(); Revision++; }
    public void Suspend() { EnsureMutable(); Status = OrganisationStatus.Suspended; Revision++; }
    public void Activate() { if (Status == OrganisationStatus.Archived) throw new InvalidOperationException("Archived organisations cannot be activated."); Status = OrganisationStatus.Active; Revision++; }
    public void Archive(bool hasActiveWorkspaces) { EnsureMutable(); if (hasActiveWorkspaces) throw new InvalidOperationException("An organisation with active workspaces cannot be archived."); Status = OrganisationStatus.Archived; Revision++; }
    private void EnsureMutable() { if (Status == OrganisationStatus.Archived) throw new InvalidOperationException("Archived organisations are immutable."); }
}

public sealed class Workspace
{
    public Guid Id { get; }
    public Guid OrganisationId { get; }
    public string Name { get; private set; }
    public string Slug { get; }
    public string Description { get; private set; }
    public WorkspaceStatus Status { get; private set; }
    public long Revision { get; private set; }

    public Workspace(Guid id, Guid organisationId, string name, string slug, string description, WorkspaceStatus status = WorkspaceStatus.Active, long revision = 1)
    {
        if (id == Guid.Empty || organisationId == Guid.Empty) throw new ArgumentException("Workspace and organisation ids are required.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Workspace name and slug are required.");
        Id = id; OrganisationId = organisationId; Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant(); Description = description?.Trim() ?? ""; Status = status; Revision = revision;
    }

    public void Update(string name, string description) { EnsureMutable(); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Workspace name is required."); Name = name.Trim(); Description = description?.Trim() ?? ""; Revision++; }
    public void Suspend() { EnsureMutable(); Status = WorkspaceStatus.Suspended; Revision++; }
    public void Activate(bool organisationActive) { if (!organisationActive) throw new InvalidOperationException("The organisation must be active."); if (Status == WorkspaceStatus.Archived) throw new InvalidOperationException("Archived workspaces cannot be activated."); Status = WorkspaceStatus.Active; Revision++; }
    public void Archive() { EnsureMutable(); Status = WorkspaceStatus.Archived; Revision++; }
    private void EnsureMutable() { if (Status == WorkspaceStatus.Archived) throw new InvalidOperationException("Archived workspaces are immutable."); }
}

public sealed class WorkspaceMembership
{
    public Guid Id { get; }
    public Guid WorkspaceId { get; }
    public Guid UserId { get; }
    public WorkspaceRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public long Revision { get; private set; }
    public WorkspaceMembership(Guid id, Guid workspaceId, Guid userId, WorkspaceRole role, MembershipStatus status, long revision = 1)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Membership, workspace, and user ids are required.");
        (Id, WorkspaceId, UserId, Role, Status, Revision) = (id, workspaceId, userId, role, status, revision);
    }
    public void ChangeRole(WorkspaceRole role, bool removingFinalAdministrator) { EnsureCurrent(); if (removingFinalAdministrator) throw new InvalidOperationException("Every workspace must retain an active Administrator."); Role = role; Revision++; }
    public void Activate() { if (Status == MembershipStatus.Removed) throw new InvalidOperationException("Removed memberships cannot be activated."); Status = MembershipStatus.Active; Revision++; }
    public void Suspend(bool finalAdministrator) { EnsureCurrent(); if (finalAdministrator) throw new InvalidOperationException("The final Administrator cannot be suspended."); Status = MembershipStatus.Suspended; Revision++; }
    public void Remove(bool finalAdministrator) { EnsureCurrent(); if (finalAdministrator) throw new InvalidOperationException("The final Administrator cannot be removed."); Status = MembershipStatus.Removed; Revision++; }
    private void EnsureCurrent() { if (Status == MembershipStatus.Removed) throw new InvalidOperationException("Removed memberships are immutable."); }
}

public sealed class IdentityUser
{
    public Guid Id { get; }
    public string Email { get; }
    public string DisplayName { get; private set; }
    public IdentityUserStatus Status { get; private set; }
    public long Revision { get; private set; }

    public IdentityUser(Guid id, string email, string displayName, IdentityUserStatus status, long revision = 1)
    {
        if (id == Guid.Empty) throw new ArgumentException("User id is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("A valid email address is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.");
        Id = id;
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Status = status;
        Revision = revision;
    }

    public void Activate() { if (Status == IdentityUserStatus.Disabled) throw new InvalidOperationException("Disabled users cannot be activated."); Status = IdentityUserStatus.Active; Revision++; }
    public void Suspend() { if (Status == IdentityUserStatus.Disabled) throw new InvalidOperationException("Disabled users are immutable."); Status = IdentityUserStatus.Suspended; Revision++; }
    public void Disable() { Status = IdentityUserStatus.Disabled; Revision++; }
}

public sealed class ServiceAccount
{
    public Guid Id { get; }
    public Guid WorkspaceId { get; }
    public string Name { get; private set; }
    public ServiceAccountStatus Status { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public long Revision { get; private set; }

    public ServiceAccount(Guid id, Guid workspaceId, string name, ServiceAccountStatus status, DateTimeOffset? expiresAt, long revision = 1)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty) throw new ArgumentException("Service account and workspace ids are required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Service account name is required.");
        Id = id; WorkspaceId = workspaceId; Name = name.Trim(); Status = status; ExpiresAt = expiresAt; Revision = revision;
    }

    public void Rotate(DateTimeOffset? expiresAt) { EnsureActive(); ExpiresAt = expiresAt; Revision++; }
    public void Revoke() { EnsureActive(); Status = ServiceAccountStatus.Revoked; Revision++; }
    private void EnsureActive() { if (Status != ServiceAccountStatus.Active) throw new InvalidOperationException("Only active service accounts can be changed."); }
}

public static class WorkspacePermissions
{
    public const string WorkspaceMember = "WorkspaceMember";
    public const string ManageWorkspace = "CanManageWorkspace";
    public const string ManageMembers = "CanManageMembers";
    public const string ManageServiceAccounts = "CanManageServiceAccounts";
    public const string CreateAssets = "CanCreateAssets";
    public const string EditAssets = "CanEditAssets";
    public const string PublishAssets = "CanPublishAssets";
    public const string ReviewAssets = "CanReviewAssets";
    public const string RunSimulation = "CanRunSimulation";
    public const string RunReplay = "CanRunReplay";
    public const string InspectSensitiveTrace = "CanInspectSensitiveTrace";
    public const string ManagePolicies = "CanManagePolicies";
    public const string DraftPolicies = "CanDraftPolicies";
    public const string ManagePlugins = "CanManagePlugins";
    public const string DraftPlugins = "CanDraftPlugins";
    public const string CreateEvaluations = "CanCreateEvaluations";
    public const string ReviewEvaluations = "CanReviewEvaluations";
    public const string CompleteReplay = "CanCompleteReplay";
    public const string ViewOperationalTrace = "CanViewOperationalTrace";
    public const string ViewPolicyDecisions = "CanViewPolicyDecisions";
    public const string CheckProviderHealth = "CanCheckProviderHealth";

    public static IReadOnlySet<string> For(WorkspaceRole role) => role switch
    {
        WorkspaceRole.Administrator => All,
        WorkspaceRole.Engineer => new HashSet<string> { WorkspaceMember, CreateAssets, EditAssets, RunSimulation, RunReplay, CreateEvaluations, InspectSensitiveTrace, ViewOperationalTrace, ViewPolicyDecisions, DraftPolicies, DraftPlugins },
        WorkspaceRole.Reviewer => new HashSet<string> { WorkspaceMember, ReviewAssets, PublishAssets, ReviewEvaluations, CompleteReplay, InspectSensitiveTrace, ViewOperationalTrace, ViewPolicyDecisions, ManagePolicies },
        WorkspaceRole.Operator => new HashSet<string> { WorkspaceMember, RunSimulation, CreateEvaluations, ViewOperationalTrace, ViewPolicyDecisions, CheckProviderHealth },
        _ => new HashSet<string> { WorkspaceMember }
    };

    private static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        WorkspaceMember, ManageWorkspace, ManageMembers, ManageServiceAccounts, CreateAssets, EditAssets,
        PublishAssets, ReviewAssets, RunSimulation, RunReplay, InspectSensitiveTrace, ManagePolicies,
        DraftPolicies, ManagePlugins, DraftPlugins, CreateEvaluations, ReviewEvaluations, CompleteReplay,
        ViewOperationalTrace, ViewPolicyDecisions, CheckProviderHealth
    };
}
