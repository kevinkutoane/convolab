using System.Text.RegularExpressions;

namespace ConvoLab.Api.Middleware;

/// <summary>
/// Inspects outgoing <c>application/problem+json</c> responses and redacts any field
/// values that match known sensitive patterns (tokens, subjects, email addresses,
/// authorization codes, secrets, nonces).
///
/// This is a defence-in-depth control that enforces the prohibition stated in
/// ExternalIdentitySecurity.md at the transport layer rather than relying solely
/// on call-site discipline.
/// </summary>
public sealed partial class SensitiveOutputSanitizerMiddleware(
    RequestDelegate next,
    ILogger<SensitiveOutputSanitizerMiddleware> logger)
{
    // Matches JSON string values that look like:
    //   - JWT tokens        (three dot-delimited base64url segments)
    //   - OAuth codes       (short alphanumeric with optional hyphens ~40 chars)
    //   - Email addresses
    //   - Likely secrets    (keys containing "secret", "password", "credential", "token", "nonce", "subject")
    private static readonly string[] SensitiveKeys =
    [
        "secret", "password", "credential", "token", "nonce",
        "subject", "code", "authorization", "access_token",
        "id_token", "refresh_token", "client_secret"
    ];

    // Matches a JWT-shaped value: header.payload.signature (all base64url)
    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.Compiled)]
    private static partial Regex JwtPattern();

    // Matches a simple email address in a JSON string value
    [GeneratedRegex(@"""[^""]*@[^""]{1,253}\.[^""]{2,63}""", RegexOptions.Compiled)]
    private static partial Regex EmailInJsonPattern();

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var contentType = context.Response.ContentType ?? string.Empty;

        if (IsProblemJson(contentType) && buffer.Length > 0 && buffer.Length < 65_536)
        {
            var body = await new StreamReader(buffer).ReadToEndAsync();
            var sanitised = Sanitise(body, context.TraceIdentifier);
            var sanitisedBytes = System.Text.Encoding.UTF8.GetBytes(sanitised);
            context.Response.ContentLength = sanitisedBytes.Length;
            await originalBody.WriteAsync(sanitisedBytes, context.RequestAborted);
        }
        else
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }
    }

    private string Sanitise(string body, string correlationId)
    {
        var changed = false;

        // Redact JWT-shaped values anywhere in the body
        var result = JwtPattern().Replace(body, match =>
        {
            changed = true;
            return "[REDACTED]";
        });

        // Redact bare email addresses in JSON string values
        result = EmailInJsonPattern().Replace(result, match =>
        {
            changed = true;
            return "\"[REDACTED]\"";
        });

        // Redact known sensitive key-value pairs, e.g. "token":"abc123"
        foreach (var key in SensitiveKeys)
        {
            var keyPattern = new Regex(
                $@"""(?i:{Regex.Escape(key)})""\s*:\s*""[^""]+""",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            result = keyPattern.Replace(result, m =>
            {
                changed = true;
                return $"\"{key}\":\"[REDACTED]\"";
            });
        }

        if (changed)
        {
            logger.LogWarning(
                "SensitiveOutputSanitizer redacted sensitive field(s) from problem+json response. " +
                "CorrelationId={CorrelationId}. Investigate the originating exception or error handler.",
                correlationId);
        }

        return result;
    }

    private static bool IsProblemJson(string contentType) =>
        contentType.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
}
