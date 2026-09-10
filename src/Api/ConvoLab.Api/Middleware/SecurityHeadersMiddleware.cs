namespace ConvoLab.Api.Middleware;

/// <summary>
/// Applies a hardened set of HTTP security response headers on every request.
/// Headers are applied before any controller or downstream middleware writes the response,
/// so they are present even on error and redirect responses.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    // HSTS is emitted by ASP.NET's UseHsts() in production; we include it here only as a
    // safeguard for environments that do not call UseHsts() (e.g. non-production deployments
    // running behind a TLS-terminating proxy). UseHsts() takes precedence when both are used.
    private const int HstsMaxAgeSeconds = 63_072_000; // 2 years

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Deny framing (clickjacking)
        headers["X-Frame-Options"] = "DENY";

        // Suppress referrer information on cross-origin navigation
        headers["Referrer-Policy"] = "no-referrer";

        // Restrict browser feature access
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

        // Prevent cross-origin document access / opener relationship
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        // Require CORP for all sub-resources (defence-in-depth for Spectre-class attacks)
        headers["Cross-Origin-Embedder-Policy"] = "require-corp";

        // Deny cross-domain Flash / Silverlight / PDF plugin access
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        // Content Security Policy — no inline scripts/styles; hashed external Vite bundles
        // satisfy 'self'. 'unsafe-inline' is intentionally absent for scripts.
        // style-src includes 'unsafe-inline' because React's style={{...}} props set CSSOM styles
        // (not <style> tags), but Belt-and-suspenders allows the few cases that do use style tags.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        // HSTS — 2 years, includeSubDomains, preload-eligible
        // Only emit in non-development environments; UseHsts() handles the Production case.
        if (!environment.IsDevelopment())
        {
            headers["Strict-Transport-Security"] =
                $"max-age={HstsMaxAgeSeconds}; includeSubDomains; preload";
        }

        await next(context);
    }
}
