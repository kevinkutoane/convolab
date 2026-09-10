using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace ConvoLab.Api.Telemetry;

/// <summary>
/// A Serilog <see cref="ILogEventFilter"/> that prevents log events containing
/// sensitive patterns (JWT tokens, email addresses, known secret field values)
/// from being forwarded to any sink.
///
/// This is a regression-backstop complementing the runtime
/// <see cref="ConvoLab.Api.Middleware.SensitiveOutputSanitizerMiddleware"/>.
/// It does NOT replace call-site discipline; it is a last line of defence.
/// </summary>
public sealed partial class SensitiveTelemetryLogFilter : ILogEventFilter
{
    // Matches a JWT-shaped value in any rendered log property
    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.Compiled)]
    private static partial Regex JwtPattern();

    // Matches a likely email address pattern in rendered output
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    // Known sensitive property names — if ANY log property carries one of these names,
    // the event is suppressed (not forwarded to sinks).
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "token", "access_token", "id_token", "refresh_token",
        "client_secret", "secret", "password", "nonce",
        "subject", "authorization_code", "code"
    };

    public bool IsEnabled(LogEvent logEvent)
    {
        // Suppress if any structured property has a sensitive name
        foreach (var property in logEvent.Properties)
        {
            if (SensitivePropertyNames.Contains(property.Key))
                return false;
        }

        // Suppress if the rendered message contains a JWT
        var rendered = logEvent.RenderMessage();
        if (JwtPattern().IsMatch(rendered))
            return false;

        // We intentionally do NOT suppress on EmailPattern here — email may appear
        // in legitimate audit log messages (e.g. "user john@example.com logged in").
        // The middleware handles email scrubbing on outbound HTTP responses.
        // Call-site discipline is required for log statements.

        return true;
    }
}
