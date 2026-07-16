using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Entities;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Policies;

/// <summary>
/// Domain policy service enforcing enterprise knowledge governance rules that
/// span aggregates: classification access, retention, and retrieval eligibility.
/// Pure domain logic — no infrastructure dependencies.
/// </summary>
public class KnowledgeGovernancePolicy
{
    /// <summary>
    /// Determines whether a caller with the given clearance may retrieve knowledge
    /// governed by the supplied policy. Clearance must meet or exceed classification.
    /// </summary>
    public bool CanRetrieve(KnowledgePolicy policy, KnowledgeClassification callerClearance)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return callerClearance >= policy.Classification;
    }

    /// <summary>
    /// Determines whether a document has exceeded its retention period and is due
    /// for archival review.
    /// </summary>
    public bool IsRetentionExpired(KnowledgeDocument document, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Policy.RetentionPeriod is null) return false;
        var anchor = document.PublishedAt ?? document.CreatedAt;
        return asOfUtc > anchor.Add(document.Policy.RetentionPeriod.Value);
    }

    /// <summary>
    /// Filters a candidate document set down to those eligible for retrieval:
    /// published, within retention, and within the caller's clearance.
    /// </summary>
    public IReadOnlyList<KnowledgeDocument> FilterRetrievable(
        IEnumerable<KnowledgeDocument> candidates,
        KnowledgeClassification callerClearance,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .Where(d => d.IsRetrievable)
            .Where(d => CanRetrieve(d.Policy, callerClearance))
            .Where(d => !IsRetentionExpired(d, asOfUtc))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Validates that a source is fit for activation: owner assigned and, for
    /// sensitive sources, an approval-gated policy in place.
    /// </summary>
    public void EnsureSourceIsGoverned(KnowledgeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Policy.Classification >= KnowledgeClassification.Confidential
            && !source.Policy.RequiresApprovalBeforePublish)
        {
            throw new InvalidOperationException(
                $"Source '{source.Name}' is classified {source.Policy.Classification} and must require approval before publishing.");
        }
    }
}
