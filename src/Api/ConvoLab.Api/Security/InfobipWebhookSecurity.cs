using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ConvoLab.Api.Security;

public static class InfobipWebhookSecurity
{
    public const string SignatureHeader = "X-Infobip-Signature";
    public const string CallbackSecretHeader = "X-Callback-Secret";
    public const string WebhookSecretHeader = "X-Webhook-Secret";
    public const string InfobipSecretHeader = "X-Infobip-Secret";

    public static string ComputeHmacSha256Hex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeHmacSha256Base64(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyWebhookRequest(HttpRequest request, string rawBody, string expectedSecret)
    {
        if (string.IsNullOrWhiteSpace(expectedSecret))
            return false;

        var expectedSecretBytes = Encoding.UTF8.GetBytes(expectedSecret.Trim());
        return HasValidSecretHeader(request, expectedSecretBytes)
            || HasValidAuthorizationHeader(request, expectedSecretBytes)
            || HasValidSignature(request, rawBody, expectedSecretBytes);
    }

    private static bool HasValidSecretHeader(HttpRequest request, byte[] expectedSecretBytes)
    {
        string[] secretHeaders = [CallbackSecretHeader, WebhookSecretHeader, InfobipSecretHeader];
        foreach (var headerName in secretHeaders)
        {
            if (request.Headers.TryGetValue(headerName, out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
            {
                var providedBytes = Encoding.UTF8.GetBytes(headerVal.ToString().Trim());
                if (CryptographicOperations.FixedTimeEquals(providedBytes, expectedSecretBytes))
                    return true;
            }
        }

        return false;
    }

    private static bool HasValidAuthorizationHeader(HttpRequest request, byte[] expectedSecretBytes)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authVal) || string.IsNullOrWhiteSpace(authVal))
            return false;

        var authStr = authVal.ToString().Trim();
        if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            authStr = authStr["Bearer ".Length..].Trim();

        var authBytes = Encoding.UTF8.GetBytes(authStr);
        return CryptographicOperations.FixedTimeEquals(authBytes, expectedSecretBytes);
    }

    private static bool HasValidSignature(HttpRequest request, string rawBody, byte[] expectedSecretBytes)
    {
        string[] signatureHeaders = [SignatureHeader, "X-Signature", "X-Hub-Signature-256"];
        foreach (var sigHeader in signatureHeaders)
        {
            if (request.Headers.TryGetValue(sigHeader, out var sigVal) && !string.IsNullOrWhiteSpace(sigVal))
            {
                var sigStr = sigVal.ToString().Trim();
                if (sigStr.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
                    sigStr = sigStr["sha256=".Length..].Trim();

                using var hmac = new HMACSHA256(expectedSecretBytes);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
                var computedHex = Convert.ToHexString(hash);
                var computedBase64 = Convert.ToBase64String(hash);

                if (string.Equals(computedHex, sigStr, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(computedBase64, sigStr, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
