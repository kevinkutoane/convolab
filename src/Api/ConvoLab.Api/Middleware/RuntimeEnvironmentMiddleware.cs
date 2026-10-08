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
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var isStrict = RequiresStrictEnvironment(context.Request);

        if (!runtime.WorkspaceId.HasValue || !runtime.OrganisationId.HasValue)
        {
            if (isStrict)
                throw new ResourceConflictException("environment.default_unavailable", "An active workspace is required to resolve a runtime environment.");

            await next(context);
            return;
        }

        var supplied = context.Request.Headers[RequestHeaderName].ToString();
        Guid? requestedId = null;
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            if (!Guid.TryParse(supplied, out var parsed))
            {
                if (isStrict)
                    throw new RequestValidationException("runtime_environment.invalid", "The runtime environment header must contain a valid GUID.");
            }
            else
            {
                requestedId = parsed;
            }
        }

        var query = db.RuntimeEnvironments.AsNoTracking().Where(item =>
            item.WorkspaceId == runtime.WorkspaceId.Value
            && item.OrganisationId == runtime.OrganisationId.Value);
        var environment = requestedId.HasValue
            ? await query.SingleOrDefaultAsync(item => item.Id == requestedId.Value, context.RequestAborted)
            : await query.SingleOrDefaultAsync(item => item.IsDefault && item.Status == "Active", context.RequestAborted);

        if (environment is null && requestedId.HasValue && isStrict)
            throw new ResourceNotFoundException("environment.not_found", $"Environment '{requestedId}' was not found.");
        if (environment is null && isStrict)
            throw new ResourceConflictException("environment.default_unavailable", "The workspace has no active default runtime environment.");
        if (environment is not null && !string.Equals(environment.Status, "Active", StringComparison.Ordinal) && isStrict)
            throw new ResourceConflictException("environment.inactive", $"Environment '{environment.Id}' is not active.");

        if (environment is not null && string.Equals(environment.Status, "Active", StringComparison.Ordinal))
        {
            runtime.EnvironmentId = environment.Id;
            runtime.EnvironmentName = environment.Name;
            runtime.EnvironmentType = environment.EnvironmentType;
            runtime.EnvironmentResolution = requestedId.HasValue
                ? RuntimeEnvironmentResolution.Explicit
                : RuntimeEnvironmentResolution.Default;
            context.Response.Headers[ResponseHeaderName] = environment.Id.ToString();
        }

        await next(context);
    }

    private static bool RequiresStrictEnvironment(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) return false;
        var path = request.Path.Value ?? string.Empty;
        return path.StartsWith("/api/simulations", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/evaluation", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/replay", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/plugins", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/intelligence", StringComparison.OrdinalIgnoreCase);
    }
}
