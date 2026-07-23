using ConvoLab.Api.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace ConvoLab.Api.Middleware;

public sealed class CookieAntiforgeryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var unsafeMethod = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method) && !HttpMethods.IsTrace(context.Request.Method);
        var exempt = context.Request.Path.StartsWithSegments("/api/auth/login")
            || context.Request.Path.StartsWithSegments("/api/auth/invitations/accept");
        if (unsafeMethod && !exempt && context.Request.Cookies.ContainsKey(ConvoLabAuthentication.SessionCookie))
            await antiforgery.ValidateRequestAsync(context);
        await next(context);
    }
}
