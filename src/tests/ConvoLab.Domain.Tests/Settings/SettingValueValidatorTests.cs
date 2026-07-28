using ConvoLab.Domain.Settings;

namespace ConvoLab.Domain.Tests.Settings;

/// <summary>
/// Tests for the pure domain validation service that guards every setting write:
/// typed value checks, range rules, enum membership, secret-leak rejection,
/// and the protected-key registry used by Production safeguards.
/// </summary>
public sealed class SettingValueValidatorTests
{
    private static SettingDefinition Definition(
        SettingValueType type,
        string? validationRules = null,
        string? allowedValues = null,
        bool isSecret = false,
        bool isRequired = false) =>
        new("test.key", "Test", "Test Setting", "A test setting.",
            type, defaultValue: null, isSecret: isSecret, isRequired: isRequired,
            validationRules: validationRules, allowedValues: allowedValues);

    // ─── Boolean ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("\"true\"")]
    public void Boolean_accepts_valid_values(string value)
    {
        var result = SettingValueValidator.Validate(Definition(SettingValueType.Boolean), value);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Boolean_rejects_non_boolean()
    {
        var result = SettingValueValidator.Validate(Definition(SettingValueType.Boolean), "\"maybe\"");
        Assert.False(result.IsValid);
    }

    // ─── Integer / range rules ───────────────────────────────────────────────

    [Fact]
    public void Integer_enforces_min_and_max_from_validation_rules()
    {
        var def = Definition(SettingValueType.Integer, validationRules: "{\"min\":1,\"max\":10}");

        Assert.True(SettingValueValidator.Validate(def, "5").IsValid);
        Assert.False(SettingValueValidator.Validate(def, "0").IsValid);
        Assert.False(SettingValueValidator.Validate(def, "11").IsValid);
    }

    [Fact]
    public void Integer_rejects_decimal_input()
    {
        var result = SettingValueValidator.Validate(Definition(SettingValueType.Integer), "3.5");
        Assert.False(result.IsValid);
        Assert.Contains("whole number", result.Message);
    }

    // ─── Decimal (e.g. ai.temperature) ───────────────────────────────────────

    [Fact]
    public void Decimal_enforces_temperature_style_range()
    {
        var def = Definition(SettingValueType.Decimal, validationRules: "{\"min\":0,\"max\":2}");

        Assert.True(SettingValueValidator.Validate(def, "0.7").IsValid);
        Assert.False(SettingValueValidator.Validate(def, "9.9").IsValid);
        Assert.Contains("at most 2", SettingValueValidator.Validate(def, "9.9").Message);
    }

    // ─── Percentage / Currency / Duration ────────────────────────────────────

    [Theory]
    [InlineData("0")]
    [InlineData("0.5")]
    [InlineData("1")]
    public void Percentage_accepts_zero_to_one(string value) =>
        Assert.True(SettingValueValidator.Validate(Definition(SettingValueType.Percentage), value).IsValid);

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    public void Percentage_rejects_out_of_band(string value) =>
        Assert.False(SettingValueValidator.Validate(Definition(SettingValueType.Percentage), value).IsValid);

    [Fact]
    public void Currency_rejects_negative_amounts() =>
        Assert.False(SettingValueValidator.Validate(Definition(SettingValueType.Currency), "-10").IsValid);

    [Fact]
    public void Duration_rejects_negative_values() =>
        Assert.False(SettingValueValidator.Validate(Definition(SettingValueType.Duration), "-1").IsValid);

    // ─── Enum membership ─────────────────────────────────────────────────────

    [Fact]
    public void Enum_enforces_allowed_values_case_insensitively()
    {
        var def = Definition(SettingValueType.Enum, allowedValues: "[\"Gemini\",\"OpenAI\"]");

        Assert.True(SettingValueValidator.Validate(def, "\"gemini\"").IsValid);
        Assert.False(SettingValueValidator.Validate(def, "\"Claude\"").IsValid);
    }

    // ─── JSON ────────────────────────────────────────────────────────────────

    [Fact]
    public void Json_requires_parseable_document()
    {
        Assert.True(SettingValueValidator.Validate(Definition(SettingValueType.Json), "{\"a\":1}").IsValid);
        Assert.False(SettingValueValidator.Validate(Definition(SettingValueType.Json), "not json").IsValid);
    }

    // ─── Secret references ───────────────────────────────────────────────────

    [Theory]
    [InlineData("\"env:GEMINI_API_KEY\"")]
    [InlineData("\"vault:kv/data/ai#api_key\"")]
    public void SecretReference_accepts_supported_providers(string value) =>
        Assert.True(SettingValueValidator.Validate(Definition(SettingValueType.SecretReference), value).IsValid);

    [Fact]
    public void SecretReference_warns_on_unknown_provider()
    {
        var result = SettingValueValidator.Validate(Definition(SettingValueType.SecretReference), "\"custom:SOME_KEY\"");
        Assert.True(result.IsValid);
        Assert.Equal("Warning", result.Status);
    }

    // ─── Secret-leak heuristics ──────────────────────────────────────────────

    [Theory]
    [InlineData("sk-abcdefghijklmnop1234567890")]
    [InlineData("AIzaSyA1234567890abcdefghijklmn")]
    [InlineData("ghp_abcdefghijklmnopqrst1234")]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    public void String_rejects_values_shaped_like_credentials(string credential)
    {
        var result = SettingValueValidator.Validate(Definition(SettingValueType.String), $"\"{credential}\"");
        Assert.False(result.IsValid);
        Assert.Contains("secret", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void String_accepts_ordinary_text() =>
        Assert.True(SettingValueValidator.Validate(Definition(SettingValueType.String), "\"gemini-2.5-flash\"").IsValid);

    [Fact]
    public void Required_string_rejects_empty_value() =>
        Assert.False(SettingValueValidator.Validate(Definition(SettingValueType.String, isRequired: true), "\"\"").IsValid);

    [Fact]
    public void Empty_input_is_rejected() =>
        Assert.False(SettingValueValidator.Validate(Definition(SettingValueType.String), " ").IsValid);

    // ─── Protected keys (Production safeguards) ──────────────────────────────

    [Theory]
    [InlineData("policy.enforcement_enabled")]
    [InlineData("policy.require_before_provider")]
    [InlineData("feature.policy_enforcement")]
    public void Enforcement_keys_are_protected(string key) =>
        Assert.True(ProtectedSettingKeys.IsEnforcementKey(key));

    [Fact]
    public void Ordinary_keys_are_not_protected() =>
        Assert.False(ProtectedSettingKeys.IsEnforcementKey("ai.temperature"));
}
