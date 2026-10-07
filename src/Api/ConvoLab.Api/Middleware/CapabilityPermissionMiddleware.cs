using ConvoLab.Domain.WorkspaceIdentity;

namespace ConvoLab.Api.Middleware;

public sealed class CapabilityPermissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (!path.StartsWith("/api/", StringComparison.Ordinal) || path.StartsWith("/api/auth") || path.StartsWith("/api/audit") || path.StartsWith("/api/workspaces") || path.StartsWith("/api/organisations") || path.StartsWith("/api/platform") || path.StartsWith("/api/operations") || path.StartsWith("/api/connectors"))
        { await next(context); return; }
        var required = RequiredPermission(context.Request.Method, path);
        if (required is not null && !context.User.HasClaim("permission", required))
        { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        await next(context);
    }

    private static string? RequiredPermission(string method, string path)
    {
        if (HttpMethods.IsGet(method)) return WorkspacePermissions.WorkspaceMember;
        if (path.StartsWith("/api/simulations", StringComparison.Ordinal) || path.StartsWith("/api/intelligence", StringComparison.Ordinal))
            return WorkspacePermissions.RunSimulation;
        if (path.StartsWith("/api/replay", StringComparison.Ordinal))
            return ResolveReplayPermission(path);
        if (path.StartsWith("/api/evaluation", StringComparison.Ordinal))
            return ResolveEvaluationPermission(path);
        if (path.StartsWith("/api/policies", StringComparison.Ordinal))
            return ResolvePolicyPermission(path);
        if (path.StartsWith("/api/plugins", StringComparison.Ordinal))
            return ResolvePluginPermission(path);
        if (IsPublishOrApprovalPath(path))
            return WorkspacePermissions.PublishAssets;
        return WorkspacePermissions.EditAssets;
    }

    private static string ResolveReplayPermission(string path) =>
        path.EndsWith("/complete", StringComparison.Ordinal) ? WorkspacePermissions.CompleteReplay : WorkspacePermissions.RunReplay;

    private static string ResolveEvaluationPermission(string path) =>
        path.Contains("/review", StringComparison.Ordinal) || path.Contains("/publish", StringComparison.Ordinal)
            ? WorkspacePermissions.ReviewEvaluations
            : WorkspacePermissions.CreateEvaluations;

    private static string ResolvePolicyPermission(string path) =>
        path.Contains("/activate", StringComparison.Ordinal) || path.Contains("/suspend", StringComparison.Ordinal) || path.Contains("/retire", StringComparison.Ordinal)
            ? WorkspacePermissions.ManagePolicies
            : WorkspacePermissions.DraftPolicies;

    private static string ResolvePluginPermission(string path) =>
        path.Contains("/activate", StringComparison.Ordinal) || path.Contains("/deprecate", StringComparison.Ordinal)
            ? WorkspacePermissions.ManagePlugins
            : WorkspacePermissions.DraftPlugins;

    private static bool IsPublishOrApprovalPath(string path) =>
        path.Contains("/publish", StringComparison.Ordinal) || path.Contains("/approve", StringComparison.Ordinal) || path.Contains("/reject", StringComparison.Ordinal);
}
