using System.Text;
using System.Text.RegularExpressions;

namespace ConvoLab.Domain.Privacy;

/// <summary>Categories of personal data the redaction engine can detect.</summary>
public enum PiiEntityType
{
    Email,
    CreditCardNumber,
    SouthAfricanIdNumber,
    Iban,
    IpAddress,
    PhoneNumber
}

/// <summary>A single detected entity; the original value never leaves the mapping.</summary>
public sealed record PiiTokenMapping(string Token, PiiEntityType EntityType, string OriginalValue);

/// <summary>
/// Result of redacting a text. <see cref="Mappings"/> holds the original values and
/// must stay in-process; only <see cref="RedactedText"/> may be sent to external providers.
/// </summary>
public sealed class RedactionResult
{
    public RedactionResult(string redactedText, IReadOnlyList<PiiTokenMapping> mappings)
    {
        RedactedText = redactedText;
        Mappings = mappings;
    }

    public string RedactedText { get; }
    public IReadOnlyList<PiiTokenMapping> Mappings { get; }
    public int RedactionCount => Mappings.Count;

    /// <summary>Per-type counts, safe to log (contains no values).</summary>
    public IReadOnlyDictionary<PiiEntityType, int> CountsByType =>
        Mappings.GroupBy(m => m.EntityType).ToDictionary(g => g.Key, g => g.Count());
}

public interface IPiiRedactionEngine
{
    RedactionResult Redact(string text);

    /// <summary>Replaces surrogate tokens in <paramref name="text"/> with their original values.</summary>
    string Restore(string text, IReadOnlyList<PiiTokenMapping> mappings);
}

/// <summary>
/// Deterministic regex-based engine. Identical values map to the same token within one
/// call, so the model can still reason about "the same email" without seeing it.
/// Patterns are ordered most-specific first; matched spans are never re-matched.
/// </summary>
public sealed class RegexPiiRedactionEngine : IPiiRedactionEngine
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly (PiiEntityType Type, Regex Pattern, Func<string, bool>? Validator)[] Detectors =
    [
        (PiiEntityType.Email,
            new Regex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled, MatchTimeout), null),
        (PiiEntityType.Iban,
            new Regex(@"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){2,7}(?: ?[A-Z0-9]{1,3})?\b", RegexOptions.Compiled, MatchTimeout),
            v => v.Replace(" ", "").Length is >= 15 and <= 34),
        // More specific than the generic card pattern, so it must claim 13-digit matches first.
        (PiiEntityType.SouthAfricanIdNumber,
            new Regex(@"\b\d{13}\b", RegexOptions.Compiled, MatchTimeout), IsPlausibleSouthAfricanId),
        (PiiEntityType.CreditCardNumber,
            new Regex(@"\b(?:\d[ \-]?){13,19}\b", RegexOptions.Compiled, MatchTimeout), PassesLuhn),
        (PiiEntityType.IpAddress,
            new Regex(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b", RegexOptions.Compiled, MatchTimeout), null),
        (PiiEntityType.PhoneNumber,
            new Regex(@"(?<![\w])(?:\+\d{1,3}[ \-]?)?(?:\(?0?\d{2,3}\)?[ \-]?)\d{3}[ \-]?\d{3,4}(?![\w])", RegexOptions.Compiled, MatchTimeout),
            v => v.Count(char.IsDigit) is >= 9 and <= 15)
    ];

    public RedactionResult Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return new RedactionResult(text ?? string.Empty, []);

        var claimed = new List<(int Start, int End, PiiEntityType Type, string Value)>();
        foreach (var (type, pattern, validator) in Detectors)
        {
            foreach (Match match in pattern.Matches(text))
            {
                if (validator is not null && !validator(match.Value)) continue;
                var start = match.Index;
                var end = match.Index + match.Length;
                if (claimed.Any(c => start < c.End && end > c.Start)) continue;
                claimed.Add((start, end, type, match.Value));
            }
        }

        var mappings = new List<PiiTokenMapping>();
        var tokenByValue = new Dictionary<(PiiEntityType, string), string>();
        var counters = new Dictionary<PiiEntityType, int>();
        var builder = new StringBuilder();
        var cursor = 0;
        foreach (var hit in claimed.OrderBy(c => c.Start))
        {
            builder.Append(text, cursor, hit.Start - cursor);
            var key = (hit.Type, hit.Value);
            if (!tokenByValue.TryGetValue(key, out var token))
            {
                counters[hit.Type] = counters.GetValueOrDefault(hit.Type) + 1;
                token = $"[{ToLabel(hit.Type)}_{counters[hit.Type]}]";
                tokenByValue[key] = token;
                mappings.Add(new PiiTokenMapping(token, hit.Type, hit.Value));
            }
            builder.Append(token);
            cursor = hit.End;
        }
        builder.Append(text, cursor, text.Length - cursor);
        return new RedactionResult(builder.ToString(), mappings);
    }

    public string Restore(string text, IReadOnlyList<PiiTokenMapping> mappings)
    {
        if (string.IsNullOrEmpty(text) || mappings.Count == 0) return text;
        foreach (var mapping in mappings)
            text = text.Replace(mapping.Token, mapping.OriginalValue, StringComparison.Ordinal);
        return text;
    }

    private static string ToLabel(PiiEntityType type) => type switch
    {
        PiiEntityType.Email => "EMAIL",
        PiiEntityType.CreditCardNumber => "CARD",
        PiiEntityType.SouthAfricanIdNumber => "SA_ID",
        PiiEntityType.Iban => "IBAN",
        PiiEntityType.IpAddress => "IP",
        PiiEntityType.PhoneNumber => "PHONE",
        _ => "PII"
    };

    private static bool PassesLuhn(string candidate)
    {
        var digits = candidate.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length is < 13 or > 19) return false;
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i];
            if (alternate) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    /// <summary>YYMMDD + 4-digit sequence + citizenship (0/1) + 8 + Luhn check digit.</summary>
    private static bool IsPlausibleSouthAfricanId(string candidate)
    {
        if (candidate.Length != 13 || !candidate.All(char.IsDigit)) return false;
        var month = int.Parse(candidate.AsSpan(2, 2));
        var day = int.Parse(candidate.AsSpan(4, 2));
        if (month is < 1 or > 12 || day is < 1 or > 31) return false;
        if (candidate[10] is not ('0' or '1')) return false;
        return PassesLuhn(candidate);
    }
}
