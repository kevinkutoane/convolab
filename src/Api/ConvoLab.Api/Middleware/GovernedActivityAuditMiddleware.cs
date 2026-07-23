using System.Security.Claims;
using ConvoLab.Api.Controllers;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;

namespace ConvoLab.Api.Middleware;

public sealed class GovernedActivityAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext db,
        WorkspaceRequestContext workspace)
    {
        await next(context);

        if (context.Response.StatusCode >= 400 || !IsMutation(context.Request.Method)) return;
        var activity = GovernedActivity(context.Request.Path.Value);
        if (activity is null || workspace.WorkspaceId is null) return;

        db.WorkspaceAuditEvents.Add(AuthController.Audit(
            "Workspace",
            workspace.OrganisationId,
            workspace.WorkspaceId,
            workspace.ActorType,
            workspace.UserId,
            context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.Name) ?? "Authenticated actor",
            activity.Value.Action,
            activity.Value.ResourceType,
            RouteResourceId(context),
            "Succeeded",
            context.TraceIdentifier));
        await db.SaveChangesAsync(context.RequestAborted);
    }

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static (string Action, string ResourceType)? GovernedActivity(string? value)
    {
        var path = value?.ToLowerInvariant() ?? string.Empty;
        if ((path.StartsWith("/api/prompts/") || path.StartsWith("/api/workflows/") || path.StartsWith("/api/knowledge/")) &&
            (path.EndsWith("/publish") || path.EndsWith("/approve") || path.EndsWith("/reject")))
            return ("Asset.LifecycleChanged", path.StartsWith("/api/prompts/") ? "Prompt" : path.StartsWith("/api/workflows/") ? "Workflow" : "KnowledgeCollection");
        if (path.StartsWith("/api/evaluation") && (path.Contains("/review") || path.Contains("/publish")))
            return ("Evaluation.Reviewed", "Evaluation");
        if (path.StartsWith("/api/replay/") && path.EndsWith("/complete"))
            return ("Replay.Completed", "ReplayExperiment");
        if (path.StartsWith("/api/policies/") && (path.EndsWith("/activate") || path.EndsWith("/suspend") || path.EndsWith("/retire")))
            return ("Policy.LifecycleChanged", "Policy");
        if (path.StartsWith("/api/plugins/") && (path.EndsWith("/activate") || path.EndsWith("/deactivate") || path.EndsWith("/disable") || path.EndsWith("/deprecate")))
            return ("Plugin.LifecycleChanged", "Plugin");
        return null;
    }

    private static string? RouteResourceId(HttpContext context)
    {
        foreach (var key in new[] { "id", "versionId", "experimentId", "policyId", "pluginId" })
            if (context.Request.RouteValues.TryGetValue(key, out var value)) return value?.ToString();
        return null;
    }
}
