using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Middleware;

/// <summary>
/// Resolves an environment only for execution-producing endpoints. The browser
/// selection is a hint; the database-scoped environment is authoritative.
/// </summary>
public sealed class RuntimeEnvironmentMiddleware(RequestDelegate next)
{
    public const string RequestHeaderName = "X-ConvoLab-Environment-Id";
    public const string ResponseHeaderName = "X-ConvoLab-Resolved-Environment-Id";

    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext db,
        WorkspaceRequestContext runtime)
    {
        if (context.User.Identity?.IsAuthenticated != true || !RequiresEnvironment(context.Request))
        {
            await next(context);
            return;
        }

        if (!runtime.WorkspaceId.HasValue || !runtime.OrganisationId.HasValue)
            throw new ResourceConflictException("environment.default_unavailable", "An active workspace is required to resolve a runtime environment.");

        var supplied = context.Request.Headers[RequestHeaderName].ToString();
        Guid? requestedId = null;
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            if (!Guid.TryParse(supplied, out var parsed))
                throw new RequestValidationException("runtime_environment.invalid", "The runtime environment header must contain a valid GUID.");
            requestedId = parsed;
        }

        var query = db.RuntimeEnvironments.AsNoTracking().Where(item =>
            item.WorkspaceId == runtime.WorkspaceId.Value
            && item.OrganisationId == runtime.OrganisationId.Value);
        var environment = requestedId.HasValue
            ? await query.SingleOrDefaultAsync(item => item.Id == requestedId.Value, context.RequestAborted)
            : await query.SingleOrDefaultAsync(item => item.IsDefault && item.Status == "Active", context.RequestAborted);

        if (environment is null && requestedId.HasValue)
            throw new ResourceNotFoundException("environment.not_found", $"Environment '{requestedId}' was not found.");
        if (environment is null)
            throw new ResourceConflictException("environment.default_unavailable", "The workspace has no active default runtime environment.");
        if (!string.Equals(environment.Status, "Active", StringComparison.Ordinal))
            throw new ResourceConflictException("environment.inactive", $"Environment '{environment.Id}' is not active.");

        runtime.EnvironmentId = environment.Id;
        runtime.EnvironmentName = environment.Name;
        runtime.EnvironmentType = environment.EnvironmentType;
        runtime.EnvironmentResolution = requestedId.HasValue
            ? RuntimeEnvironmentResolution.Explicit
            : RuntimeEnvironmentResolution.Default;
        context.Response.Headers[ResponseHeaderName] = environment.Id.ToString();
        await next(context);
    }

    private static bool RequiresEnvironment(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) return false;
        var path = request.Path.Value ?? string.Empty;
        return path.StartsWith("/api/simulations", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/evaluation", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/replay", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/plugins", StringComparison.OrdinalIgnoreCase);
    }
}
