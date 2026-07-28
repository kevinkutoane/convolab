using ConvoLab.Application.Common.Errors;
using ConvoLab.Infrastructure.WorkspaceIdentity;

namespace ConvoLab.Api.Middleware;

/// <summary>
/// Binds tenant identifiers in a route to the tenant context established by
/// authentication. Permission claims describe what an actor may do; this guard
/// establishes where that permission may be used.
/// </summary>
public sealed class RouteScopeAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WorkspaceRequestContext workspace)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (TryRouteGuid(context, "workspaceId", out var routeWorkspaceId)
            && workspace.WorkspaceId != routeWorkspaceId)
        {
            throw Missing("workspace", routeWorkspaceId);
        }

        if (TryRouteGuid(context, "organisationId", out var routeOrganisationId)
            && !workspace.IsPlatformAdministrator
            && workspace.OrganisationId != routeOrganisationId)
        {
            throw Missing("organisation", routeOrganisationId);
        }

        await next(context);
    }

    private static bool TryRouteGuid(HttpContext context, string key, out Guid id)
    {
        id = Guid.Empty;
        return context.Request.RouteValues.TryGetValue(key, out var value)
            && Guid.TryParse(value?.ToString(), out id);
    }

    private static ResourceNotFoundException Missing(string resource, Guid id) =>
        new($"{resource}.not_found", $"{resource} '{id}' was not found.");
}
