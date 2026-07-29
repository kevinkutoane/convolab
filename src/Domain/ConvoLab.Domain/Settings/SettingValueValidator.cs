using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ConvoLab.Domain.Settings;

/// <summary>
/// Outcome of validating a single setting value against its definition.
/// </summary>
public sealed record SettingValidationResult(bool IsValid, string Status, string? Message)
{
    public static SettingValidationResult Valid() => new(true, "Valid", null);
    public static SettingValidationResult Invalid(string message) => new(false, "Invalid", message);
    public static SettingValidationResult Warning(string message) => new(true, "Warning", message);
}

/// <summary>
/// Pure domain service that validates a raw JSON-encoded setting value against
/// its <see cref="SettingDefinition"/>: type checks, range rules from
/// <see cref="SettingDefinition.ValidationRules"/> (JSON: {"min":x,"max":y}),
/// enum membership from <see cref="SettingDefinition.AllowedValues"/>, and
/// secret-leak heuristics for non-secret text settings.
/// </summary>
public static partial class SettingValueValidator
{
    // Common API-key shapes that must never be stored as plain setting values.
    [GeneratedRegex(@"^(sk-[A-Za-z0-9_\-]{16,}|AIza[A-Za-z0-9_\-]{20,}|ya29\.[A-Za-z0-9_\-\.]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|xox[baprs]-[A-Za-z0-9\-]{10,}|AKIA[A-Z0-9]{16})$")]
    private static partial Regex ApiKeyShapeRegex();

    public static SettingValidationResult Validate(SettingDefinition definition, string valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
            return SettingValidationResult.Invalid("A value is required.");

        string raw;
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            raw = doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => doc.RootElement.GetString() ?? "",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => doc.RootElement.GetRawText(),
                JsonValueKind.Object or JsonValueKind.Array when definition.ValueType == SettingValueType.Json
                    => doc.RootElement.GetRawText(),
                _ => doc.RootElement.GetRawText()
            };
        }
        catch (JsonException)
        {
            // Tolerate bare (non-JSON-encoded) values for backwards compatibility.
            raw = valueJson.Trim();
        }

        return definition.ValueType switch
        {
            SettingValueType.Boolean => ValidateBoolean(raw),
            SettingValueType.Integer => ValidateInteger(definition, raw),
            SettingValueType.Decimal => ValidateDecimal(definition, raw),
            SettingValueType.Percentage => ValidatePercentage(definition, raw),
            SettingValueType.Currency => ValidateCurrency(definition, raw),
            SettingValueType.Duration => ValidateDuration(definition, raw),
            SettingValueType.Enum => ValidateEnum(definition, raw),
            SettingValueType.Json => ValidateJson(raw),
            SettingValueType.SecretReference => ValidateSecretReference(raw),
            _ => ValidateString(definition, raw)
        };
    }

    /// <summary>
    /// Returns true when a plain-text value for a non-secret setting looks like a
    /// credential and must be rejected (secrets belong in secret references).
    /// </summary>
    public static bool LooksLikeSecret(SettingDefinition definition, string rawValue)
    {
        if (definition.IsSecret || definition.ValueType == SettingValueType.SecretReference) return false;
        if (definition.ValueType != SettingValueType.String) return false;
        var candidate = rawValue.Trim().Trim('"');
        return ApiKeyShapeRegex().IsMatch(candidate);
    }

    private static SettingValidationResult ValidateBoolean(string raw) =>
        bool.TryParse(raw.Trim().Trim('"'), out _)
            ? SettingValidationResult.Valid()
            : SettingValidationResult.Invalid("Value must be 'true' or 'false'.");

    private static SettingValidationResult ValidateInteger(SettingDefinition def, string raw)
    {
        if (!long.TryParse(raw.Trim().Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return SettingValidationResult.Invalid("Value must be a whole number.");
        return CheckRange(def, value);
    }

    private static SettingValidationResult ValidateDecimal(SettingDefinition def, string raw)
    {
        if (!decimal.TryParse(raw.Trim().Trim('"'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return SettingValidationResult.Invalid("Value must be a number.");
        return CheckRange(def, value);
    }

    private static SettingValidationResult ValidatePercentage(SettingDefinition def, string raw)
    {
        if (!decimal.TryParse(raw.Trim().Trim('"'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return SettingValidationResult.Invalid("Value must be a number between 0 and 1.");
        if (value < 0m || value > 1m)
            return SettingValidationResult.Invalid("Percentage values must be between 0 and 1.");
        return CheckRange(def, value);
    }

    private static SettingValidationResult ValidateCurrency(SettingDefinition def, string raw)
    {
        if (!decimal.TryParse(raw.Trim().Trim('"'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return SettingValidationResult.Invalid("Value must be a monetary amount.");
        if (value < 0m)
            return SettingValidationResult.Invalid("Monetary amounts cannot be negative.");
        return CheckRange(def, value);
    }

    private static SettingValidationResult ValidateDuration(SettingDefinition def, string raw)
    {
        if (!long.TryParse(raw.Trim().Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return SettingValidationResult.Invalid("Duration must be a whole number.");
        if (value < 0)
            return SettingValidationResult.Invalid("Duration cannot be negative.");
        return CheckRange(def, value);
    }

    private static SettingValidationResult ValidateEnum(SettingDefinition def, string raw)
    {
        var allowed = ParseAllowedValues(def.AllowedValues);
        if (allowed.Count == 0) return SettingValidationResult.Valid();
        var candidate = raw.Trim().Trim('"');
        return allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase)
            ? SettingValidationResult.Valid()
            : SettingValidationResult.Invalid($"Value must be one of: {string.Join(", ", allowed)}.");
    }

    private static SettingValidationResult ValidateJson(string raw)
    {
        try { using var _ = JsonDocument.Parse(raw); return SettingValidationResult.Valid(); }
        catch (JsonException) { return SettingValidationResult.Invalid("Value must be valid JSON."); }
    }

    private static SettingValidationResult ValidateSecretReference(string raw)
    {
        try
        {
            var (provider, _) = SecretReference.ParseReference(raw.Trim().Trim('"'));
            return provider is "env" or "vault" or "aws" or "azure"
                ? SettingValidationResult.Valid()
                : SettingValidationResult.Warning($"Secret provider '{provider}' is not natively supported. Supported providers: env, vault, aws, azure.");
        }
        catch (ArgumentException ex)
        {
            return SettingValidationResult.Invalid(ex.Message);
        }
    }

    private static SettingValidationResult ValidateString(SettingDefinition def, string raw)
    {
        var candidate = raw.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(candidate) && def.IsRequired)
            return SettingValidationResult.Invalid("A non-empty value is required.");
        if (candidate.Length > 2000)
            return SettingValidationResult.Invalid("Value exceeds the maximum length of 2000 characters.");
        if (LooksLikeSecret(def, candidate))
            return SettingValidationResult.Invalid("This value looks like a credential. Store secrets as secret references, never as plain settings.");
        var allowed = ParseAllowedValues(def.AllowedValues);
        if (allowed.Count > 0 && !allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            return SettingValidationResult.Invalid($"Value must be one of: {string.Join(", ", allowed)}.");
        return SettingValidationResult.Valid();
    }

    private static SettingValidationResult CheckRange(SettingDefinition def, decimal value)
    {
        var (min, max) = ParseRange(def.ValidationRules);
        if (min.HasValue && value < min.Value)
            return SettingValidationResult.Invalid($"Value must be at least {min.Value.ToString(CultureInfo.InvariantCulture)}.");
        if (max.HasValue && value > max.Value)
            return SettingValidationResult.Invalid($"Value must be at most {max.Value.ToString(CultureInfo.InvariantCulture)}.");
        return SettingValidationResult.Valid();
    }

    private static (decimal? min, decimal? max) ParseRange(string? validationRules)
    {
        if (string.IsNullOrWhiteSpace(validationRules)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(validationRules);
            decimal? min = doc.RootElement.TryGetProperty("min", out var minEl) && minEl.ValueKind == JsonValueKind.Number ? minEl.GetDecimal() : null;
            decimal? max = doc.RootElement.TryGetProperty("max", out var maxEl) && maxEl.ValueKind == JsonValueKind.Number ? maxEl.GetDecimal() : null;
            return (min, max);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static IReadOnlyList<string> ParseAllowedValues(string? allowedValues)
    {
        if (string.IsNullOrWhiteSpace(allowedValues)) return [];
        try
        {
            using var doc = JsonDocument.Parse(allowedValues);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList(),
                JsonValueKind.String => doc.RootElement.GetString()?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                _ => []
            };
        }
        catch (JsonException)
        {
            return allowedValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}

/// <summary>
/// Keys that are protected in Production environments: changing them requires an
/// explicit reason, and disabling enforcement-style flags requires confirmation.
/// </summary>
public static class ProtectedSettingKeys
{
    public static readonly IReadOnlySet<string> EnforcementKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SettingKeys.PolicyEnforcementEnabled,
        SettingKeys.PolicyRequireBeforeProvider,
        SettingKeys.FeaturePolicyEnforcement
    };

    public static bool IsEnforcementKey(string key) => EnforcementKeys.Contains(key);
}
