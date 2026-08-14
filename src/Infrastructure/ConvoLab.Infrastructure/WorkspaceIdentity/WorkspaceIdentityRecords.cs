namespace ConvoLab.Infrastructure.WorkspaceIdentity;

public sealed class OrganisationRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class WorkspaceRecord
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class IdentityUserRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Invited";
    public bool IsPlatformAdministrator { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class WorkspaceMembershipRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Viewer";
    public string Status { get; set; } = "Invited";
    public string? InvitationTokenHash { get; set; }
    public DateTimeOffset? InvitationExpiresAt { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LocalCredentialRecord
{
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int BreakGlassFailedAttempts { get; set; }
    public DateTimeOffset? BreakGlassLockedUntil { get; set; }
    public DateTimeOffset? BreakGlassLastFailedAt { get; set; }
    public long BreakGlassRevision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AuthenticationSessionRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ActiveWorkspaceId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string AuthenticationProvider { get; set; } = "Local";
    public Guid? ExternalIdentityId { get; set; }
    public Guid SessionFamilyId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class ExternalIdentityRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "Entra";
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string? EmailAtLastLogin { get; set; }
    public string? DisplayNameAtLastLogin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DisabledAt { get; set; }
    public Guid? DisabledBy { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class ExternalIdentityInvitationRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string ExpectedTenant { get; set; } = string.Empty;
    public string ExpectedProvider { get; set; } = "Entra";
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? ConsumedByExternalIdentityId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string Status { get; set; } = "Active";
    public long Revision { get; set; } = 1;
}

public sealed class ServiceAccountRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string ScopesJson { get; set; } = "[]";
    public string Status { get; set; } = "Active";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AuditEventRecord
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = "Workspace";
    public Guid? OrganisationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string ActorDisplay { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Outcome { get; set; } = "Succeeded";
    public string DetailJson { get; set; } = "{}";
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class WorkspaceRequestContext : ConvoLab.Application.Common.Interfaces.IRuntimeRequestContext
{
    public Guid? UserId { get; set; }
    public Guid? OrganisationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? MembershipId { get; set; }
    public Guid? SessionId { get; set; }
    public string ActorType { get; set; } = "Anonymous";
    public string? Role { get; set; }
    public bool IsPlatformAdministrator { get; set; }
    public Guid? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public string? EnvironmentType { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ParentCorrelationId { get; set; }
    public ConvoLab.Application.Common.Interfaces.RuntimeEnvironmentResolution EnvironmentResolution { get; set; }
        = ConvoLab.Application.Common.Interfaces.RuntimeEnvironmentResolution.Unresolved;

    public Guid? ActorId => UserId;
    public string ActorRole => Role ?? string.Empty;
}
