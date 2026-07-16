using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Aggregates;

/// <summary>
/// The KnowledgePackage aggregate root. The governed, self-describing output of a
/// retrieval operation and the ONLY artifact the Prompt Engine may consume.
/// Knowledge is never injected directly into prompts — it always travels inside
/// a package that carries results, citations, confidence, ranking, strategy
/// provenance, and token estimates.
///
/// Core invariants:
/// - A package is immutable once sealed.
/// - Results are ordered by ranking (highest relevance first).
/// - Citations are mandatory when the strategy requires them.
/// - Token estimates are always present for prompt budgeting.
/// </summary>
public class KnowledgePackage : BaseAggregateRoot<KnowledgePackageId>
{
    public KnowledgeQuery Query { get; private set; }
    public RetrievalStrategyType StrategyUsed { get; private set; }
    public bool IsSealed { get; private set; }
    public DateTime AssembledAt { get; private set; }

    private readonly List<KnowledgeResult> _results = new();
    public IReadOnlyCollection<KnowledgeResult> Results => _results.AsReadOnly();

    private KnowledgePackage() { Query = null!; } // For EF Core

    private KnowledgePackage(KnowledgePackageId id, KnowledgeQuery query) : base(id)
    {
        Query = query;
        StrategyUsed = query.Strategy.Type;
        IsSealed = false;
        AssembledAt = DateTime.UtcNow;
    }

    /// <summary>Begins assembling a package for the given query.</summary>
    public static KnowledgePackage StartAssembly(KnowledgeQuery query)
    {
        return new KnowledgePackage(
            KnowledgePackageId.CreateUnique(),
            query ?? throw new ArgumentNullException(nameof(query)));
    }

    #region Assembly

    /// <summary>
    /// Adds a retrieved result to the package, enforcing the query's strategy
    /// constraints (max results, minimum confidence, citation requirements).
    /// </summary>
    public void AddResult(KnowledgeResult result)
    {
        EnsureNotSealed();
        ArgumentNullException.ThrowIfNull(result);

        if (_results.Count >= Query.Strategy.MaxResults)
            throw new InvalidOperationException($"Package already contains the maximum of {Query.Strategy.MaxResults} results.");

        if (result.Confidence < Query.Strategy.MinConfidence)
            throw new InvalidOperationException(
                $"Result confidence {result.Confidence:F2} is below the strategy minimum of {Query.Strategy.MinConfidence:F2}.");

        _results.Add(result);
    }

    /// <summary>
    /// Seals the package, ordering results by ranking and making the package
    /// immutable. Raises KnowledgeRetrieved and KnowledgePackageCreated events.
    /// </summary>
    public void Seal()
    {
        EnsureNotSealed();

        if (Query.Strategy.IncludeCitations && _results.Any(r => r.Citation is null))
            throw new InvalidOperationException("All results must carry citations when the strategy requires them.");

        _results.Sort((a, b) => a.Ranking.CompareTo(b.Ranking));
        IsSealed = true;

        AddDomainEvent(new KnowledgeRetrievedEvent(
            Query.Id, StrategyUsed, _results.Count, Query.ConversationId, Query.WorkflowId));

        AddDomainEvent(new KnowledgePackageCreatedEvent(
            Id, Query.Id, _results.Count, TotalEstimatedTokens));
    }

    private void EnsureNotSealed()
    {
        if (IsSealed)
            throw new InvalidOperationException("Package is sealed and immutable.");
    }

    #endregion

    #region Consumption (read-side for the Prompt Engine)

    /// <summary>Total token estimate across all results, used for prompt budgeting.</summary>
    public int TotalEstimatedTokens => _results.Sum(r => r.EstimatedTokens);

    /// <summary>All citations carried by the package, in ranked order.</summary>
    public IReadOnlyList<KnowledgeCitation> Citations =>
        _results.Select(r => r.Citation).ToList().AsReadOnly();

    /// <summary>Average confidence across results; 0 when empty.</summary>
    public double AverageConfidence =>
        _results.Any() ? _results.Average(r => r.Confidence) : 0.0;

    /// <summary>True when retrieval found nothing — consumers must handle this explicitly.</summary>
    public bool IsEmpty => !_results.Any();

    /// <summary>
    /// Returns the top results whose cumulative token estimate fits within the
    /// given budget. This is how the Prompt Engine trims knowledge to fit context.
    /// </summary>
    public IReadOnlyList<KnowledgeResult> FitToTokenBudget(int tokenBudget)
    {
        if (tokenBudget <= 0)
            throw new ArgumentException("Token budget must be positive.", nameof(tokenBudget));

        var selected = new List<KnowledgeResult>();
        var used = 0;
        foreach (var result in _results)
        {
            if (used + result.EstimatedTokens > tokenBudget) break;
            selected.Add(result);
            used += result.EstimatedTokens;
        }
        return selected.AsReadOnly();
    }

    #endregion
}
