namespace ConvoLab.Application.Common.Interfaces;

public enum RuntimeEnvironmentResolution
{
    Unresolved,
    Explicit,
    Default
}

/// <summary>
/// Trusted, request-scoped attribution established by the API after authentication.
/// Application services must not derive runtime environment or actor information
/// from process environment variables or request payloads.
/// </summary>
public interface IRuntimeRequestContext
{
    Guid? OrganisationId { get; }
    Guid? WorkspaceId { get; }
    Guid? EnvironmentId { get; }
    string? EnvironmentName { get; }
    string? EnvironmentType { get; }
    Guid? ActorId { get; }
    string ActorType { get; }
    string? ActorRole { get; }
    string CorrelationId { get; }
    string? ParentCorrelationId { get; }
    RuntimeEnvironmentResolution EnvironmentResolution { get; }
}
