using System.Text.RegularExpressions;

namespace ConvoLab.Domain.Omnichannel;

public interface IHumanHandoffEvaluator
{
    HandoffEvaluationResult Evaluate(string userText, int consecutiveFrustrations = 0);
}

/// <summary>
/// Evaluates user messages for explicit escalation requests or severe negative sentiment triggers
/// (e.g. demanding a human agent, reporting urgent legal/regulatory complaints, repeated frustration).
/// </summary>
public sealed class KeywordHumanHandoffEvaluator : IHumanHandoffEvaluator
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly (string Reason, string Department, Regex Pattern)[] EscalationRules =
        new (string Reason, string Department, Regex Pattern)[]
        {
            (
                "Explicit human agent request",
                "CustomerService",
                new Regex(@"\b(?:talk to|speak to|connect me to|transfer(?: me)? to|need a|want to speak to|want a)\s+(?:(?:a|an)\s+)?(?:human|person|agent|representative|consultant|operator|manager)\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
            ),
            (
                "Frustration or dissatisfaction escalation",
                "Escalations",
                new Regex(@"\b(?:this is ridiculous|useless bot|stupid bot|speak to someone|stop repeating|give me someone real)\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
            ),
            (
                "Legal or Ombudsman threat",
                "Compliance",
                new Regex(@"\b(?:ombudsman|lawyer|attorney|sue you|legal action|regulator|fscca|popia complaint)\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
            ),
            (
                "Urgent fraud or security report",
                "Fraud",
                new Regex(@"\b(?:stolen vehicle|card compromised|fraud alert|report fraud|unauthorized transaction)\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase, MatchTimeout)
            )
        };

    public HandoffEvaluationResult Evaluate(string userText, int consecutiveFrustrations = 0)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return new HandoffEvaluationResult(false, "No input provided.");

        if (consecutiveFrustrations >= 3)
        {
            return new HandoffEvaluationResult(
                true,
                "Consecutive unresolved conversational turns exceeded threshold (3).",
                "CustomerService",
                0.2);
        }

        foreach (var (reason, department, pattern) in EscalationRules)
        {
            if (pattern.IsMatch(userText))
            {
                return new HandoffEvaluationResult(
                    true,
                    reason,
                    department,
                    0.3);
            }
        }

        return new HandoffEvaluationResult(false, "No escalation criteria met.", null, 0.9);
    }
}
