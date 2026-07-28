using Serilog.Context;
using ConvoLab.Infrastructure.WorkspaceIdentity;

namespace ConvoLab.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ParentHeaderName = "X-Parent-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, WorkspaceRequestContext runtime)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var supplied = context.Request.Headers.TryGetValue(HeaderName, out var candidate)
            && !string.IsNullOrWhiteSpace(candidate)
                ? candidate.ToString()[..Math.Min(candidate.ToString().Length, 100)]
                : null;

        context.TraceIdentifier = correlationId;
        runtime.CorrelationId = correlationId;
        runtime.ParentCorrelationId = supplied;
        context.Response.Headers[HeaderName] = correlationId;
        if (supplied is not null) context.Response.Headers[ParentHeaderName] = supplied;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
