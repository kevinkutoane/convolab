using System.Security.Claims;
using ConvoLab.Api.Controllers;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Infrastructure.Analytics;
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
        var activity = GovernedActivity(context.Request.Path.Value);
        try
        {
            await next(context);
        }
        catch (Exception exception) when (
            activity?.Action == "Plugin.Activated"
            && workspace.WorkspaceId is not null)
        {
            db.ChangeTracker.Clear();
            var failedAction = exception is ConvoLabException error
                && error.Code.Contains("compatib", StringComparison.OrdinalIgnoreCase)
                    ? "Plugin.CompatibilityFailed"
                    : "Plugin.ActivationFailed";
            await RecordAsync(
                context,
                db,
                workspace,
                failedAction,
                "Plugin",
                "Failed");
            throw;
        }

        if (context.Response.StatusCode >= 400
            || !IsMutation(context.Request.Method)
            || activity is null
            || workspace.WorkspaceId is null)
            return;
        if (activity.Value.Action.StartsWith("Plugin.", StringComparison.Ordinal))
            return;

        await RecordAsync(
            context,
            db,
            workspace,
            activity.Value.Action,
            activity.Value.ResourceType,
            "Succeeded");
    }

    private static async Task RecordAsync(
        HttpContext context,
        ApplicationDbContext db,
        WorkspaceRequestContext workspace,
        string action,
        string resourceType,
        string outcome)
    {
        var audit = AuthController.Audit(
            "Workspace",
            workspace.OrganisationId,
            workspace.WorkspaceId,
            workspace.ActorType,
            workspace.UserId,
            context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.Name) ?? "Authenticated actor",
            action,
            resourceType,
            RouteResourceId(context),
            outcome,
            context.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(
            db,
            audit,
            workspace.EnvironmentId,
            context.RequestAborted);
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
        if (path.StartsWith("/api/plugins/") && path.EndsWith("/activate"))
            return ("Plugin.Activated", "Plugin");
        if (path.StartsWith("/api/plugins/") && path.EndsWith("/health"))
            return ("Plugin.HealthChecked", "Plugin");
        if (path.StartsWith("/api/plugins/") && (path.EndsWith("/deactivate") || path.EndsWith("/disable") || path.EndsWith("/deprecate")))
            return ("Plugin.Deactivated", "Plugin");
        return null;
    }

    private static string? RouteResourceId(HttpContext context)
    {
        foreach (var key in new[] { "id", "versionId", "experimentId", "policyId", "pluginId" })
            if (context.Request.RouteValues.TryGetValue(key, out var value)) return value?.ToString();
        return null;
    }
}
