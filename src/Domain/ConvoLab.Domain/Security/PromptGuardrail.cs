using System.Text.RegularExpressions;

namespace ConvoLab.Domain.Security;

public enum GuardrailThreatType
{
    PromptInjection,
    Jailbreak,
    SystemPromptLeak,
    BannedKeyword
}

public sealed record GuardrailViolation(
    GuardrailThreatType ThreatType,
    string PatternName,
    string Description,
    string MatchedText);

public sealed record GuardrailEvaluationResult(
    bool IsAllowed,
    string Reason,
    IReadOnlyList<GuardrailViolation> Violations);

public interface IPromptGuardrailEngine
{
    GuardrailEvaluationResult Evaluate(string text);
}

/// <summary>
/// Pre-execution safety guardrail engine that detects prompt injections, 
/// jailbreak attempts (e.g. DAN, roleplay bypass), and system instruction leakage attempts.
/// </summary>
public sealed class RegexPromptGuardrailEngine : IPromptGuardrailEngine
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly (GuardrailThreatType Threat, string PatternName, string Description, Regex Pattern)[] Rules =
    [
        (
            GuardrailThreatType.PromptInjection,
            "InstructionOverride",
            "Attempts to ignore, override, or disregard previous instructions.",
            new Regex(@"\b(?:ignore|disregard|forget|override)\s+(?:all\s+)?(?:previous|prior|above|existing)\s+(?:instructions|prompts|rules|commands|context)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
        ),
        (
            GuardrailThreatType.PromptInjection,
            "NewInstructionPrefix",
            "Attempts to inject new system directives or root instructions.",
            new Regex(@"(?:^|\n)\s*(?:system\s*:\s*|\[system\]|<<sys>>|<\|im_start\|>system)\s*",
                RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
        ),
        (
            GuardrailThreatType.Jailbreak,
            "DoAnythingNowBypass",
            "Classic jailbreak archetype attempting to remove AI safety limits (e.g. DAN / AIM / Developer Mode).",
            new Regex(@"\b(?:DAN|AIM|jailbreak|developer\s+mode|unfiltered\s+mode)\s+(?:enabled|activated|mode|persona)?\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
        ),
        (
            GuardrailThreatType.Jailbreak,
            "SafetyConstraintBypass",
            "Explicit demand to ignore ethical or safety guidelines.",
            new Regex(@"\b(?:bypass|disable|remove|break)\s+(?:all\s+)?(?:safety|ethical|content|guardrail|security)\s+(?:filters|protocols|guidelines|policies|limits)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
        ),
        (
            GuardrailThreatType.SystemPromptLeak,
            "SystemPromptExfiltration",
            "Direct query attempting to leak internal base system prompt instructions.",
            new Regex(@"\b(?:what\s+is\s+your|repeat\s+the|print\s+your|show\s+me\s+your|reveal\s+your)\s+(?:original\s+)?(?:system\s+prompt|initial\s+prompt|base\s+instructions|system\s+instructions)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
        )
    ];

    public GuardrailEvaluationResult Evaluate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new GuardrailEvaluationResult(true, "Prompt is empty.", []);

        var violations = new List<GuardrailViolation>();

        foreach (var (threat, name, description, pattern) in Rules)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                violations.Add(new GuardrailViolation(
                    threat,
                    name,
                    description,
                    match.Value));
            }
        }

        if (violations.Count > 0)
        {
            var descriptions = string.Join("; ", violations.Select(v => $"{v.ThreatType} detected ({v.PatternName}): {v.Description}"));
            return new GuardrailEvaluationResult(false, $"Prompt guardrail violation: {descriptions}", violations);
        }

        return new GuardrailEvaluationResult(true, "Prompt cleared all safety guardrails.", []);
    }
}
