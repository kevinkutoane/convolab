using ConvoLab.Domain.WorkspaceIdentity;

namespace ConvoLab.Api.Middleware;

public sealed class CapabilityPermissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (!path.StartsWith("/api/", StringComparison.Ordinal) || path.StartsWith("/api/auth") || path.StartsWith("/api/workspaces") || path.StartsWith("/api/organisations") || path.StartsWith("/api/platform"))
        { await next(context); return; }
        var required = RequiredPermission(context.Request.Method, path);
        if (required is not null && !context.User.HasClaim("permission", required))
        { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        await next(context);
    }

    private static string? RequiredPermission(string method, string path)
    {
        if (HttpMethods.IsGet(method)) return WorkspacePermissions.WorkspaceMember;
        if (path.StartsWith("/api/simulations") || path.StartsWith("/api/intelligence")) return WorkspacePermissions.RunSimulation;
        if (path.StartsWith("/api/replay")) return path.EndsWith("/complete") ? WorkspacePermissions.CompleteReplay : WorkspacePermissions.RunReplay;
        if (path.StartsWith("/api/evaluation")) return path.Contains("/review") || path.Contains("/publish") ? WorkspacePermissions.ReviewEvaluations : WorkspacePermissions.CreateEvaluations;
        if (path.StartsWith("/api/policies")) return path.Contains("/activate") || path.Contains("/suspend") || path.Contains("/retire") ? WorkspacePermissions.ManagePolicies : WorkspacePermissions.DraftPolicies;
        if (path.StartsWith("/api/plugins")) return path.Contains("/activate") || path.Contains("/deprecate") ? WorkspacePermissions.ManagePlugins : WorkspacePermissions.DraftPlugins;
        if (path.Contains("/publish") || path.Contains("/approve") || path.Contains("/reject")) return WorkspacePermissions.PublishAssets;
        return WorkspacePermissions.EditAssets;
    }
}
