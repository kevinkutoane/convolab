using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Domain.Knowledge.ValueObjects;

/// <summary>
/// A filter constraint applied during retrieval: restricts results by metadata,
/// classification, source, tag, or recency. Filters compose into strategies.
/// </summary>
public class KnowledgeFilter : ValueObject
{
    public string Field { get; private set; }
    public string Operator { get; private set; }
    public string Value { get; private set; }

    private KnowledgeFilter() { Field = null!; Operator = null!; Value = null!; } // For EF Core

    private KnowledgeFilter(string field, string @operator, string value)
    {
        Field = field;
        Operator = @operator;
        Value = value;
    }

    public static KnowledgeFilter Create(string field, string @operator, string value)
    {
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Filter field cannot be empty.", nameof(field));
        if (string.IsNullOrWhiteSpace(@operator))
            throw new ArgumentException("Filter operator cannot be empty.", nameof(@operator));
        return new KnowledgeFilter(field, @operator, value ?? string.Empty);
    }

    public static KnowledgeFilter Equals_(string field, string value) => Create(field, "eq", value);
    public static KnowledgeFilter Contains(string field, string value) => Create(field, "contains", value);
    public static KnowledgeFilter NewerThan(DateTime cutoff) => Create("modifiedAt", "gt", cutoff.ToString("O"));

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Field;
        yield return Operator;
        yield return Value;
    }
}

/// <summary>
/// The retrieval strategy a consumer selects when querying the Knowledge Engine.
/// Encapsulates strategy type, result limits, confidence thresholds, and filters.
/// The Conversation Engine never knows how retrieval is executed — only which
/// strategy contract it requested.
/// </summary>
public class KnowledgeRetrievalStrategy : ValueObject
{
    public RetrievalStrategyType Type { get; private set; }
    public int MaxResults { get; private set; }
    public double MinConfidence { get; private set; }
    public bool IncludeCitations { get; private set; }
    public IReadOnlyList<KnowledgeFilter> Filters { get; private set; }

    private KnowledgeRetrievalStrategy() { Filters = new List<KnowledgeFilter>(); } // For EF Core

    private KnowledgeRetrievalStrategy(
        RetrievalStrategyType type,
        int maxResults,
        double minConfidence,
        bool includeCitations,
        IReadOnlyList<KnowledgeFilter> filters)
    {
        Type = type;
        MaxResults = maxResults;
        MinConfidence = minConfidence;
        IncludeCitations = includeCitations;
        Filters = filters;
    }

    public static KnowledgeRetrievalStrategy Create(
        RetrievalStrategyType type,
        int maxResults = 10,
        double minConfidence = 0.0,
        bool includeCitations = true,
        IEnumerable<KnowledgeFilter>? filters = null)
    {
        if (maxResults <= 0)
            throw new ArgumentException("MaxResults must be positive.", nameof(maxResults));
        if (minConfidence is < 0.0 or > 1.0)
            throw new ArgumentException("MinConfidence must be between 0 and 1.", nameof(minConfidence));

        return new KnowledgeRetrievalStrategy(
            type, maxResults, minConfidence, includeCitations,
            (filters ?? Enumerable.Empty<KnowledgeFilter>()).ToList().AsReadOnly());
    }

    public static KnowledgeRetrievalStrategy Keyword(int maxResults = 10) => Create(RetrievalStrategyType.Keyword, maxResults);
    public static KnowledgeRetrievalStrategy Semantic(int maxResults = 10, double minConfidence = 0.5) => Create(RetrievalStrategyType.Semantic, maxResults, minConfidence);
    public static KnowledgeRetrievalStrategy Hybrid(int maxResults = 10, double minConfidence = 0.5) => Create(RetrievalStrategyType.Hybrid, maxResults, minConfidence);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Type;
        yield return MaxResults;
        yield return MinConfidence;
        yield return IncludeCitations;
        foreach (var f in Filters) yield return f;
    }
}

/// <summary>
/// A knowledge query issued by a workflow or conversation. Carries the question,
/// the requested strategy, and optional conversation/workflow context so that
/// conversation-aware and workflow-aware strategies can be honoured.
/// </summary>
public class KnowledgeQuery : ValueObject
{
    public KnowledgeQueryId Id { get; private set; }
    public string QueryText { get; private set; }
    public KnowledgeRetrievalStrategy Strategy { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? WorkflowId { get; private set; }
    public IReadOnlyList<KnowledgeCollectionId> CollectionScope { get; private set; }

    private KnowledgeQuery()
    {
        Id = null!; QueryText = null!; Strategy = null!;
        CollectionScope = new List<KnowledgeCollectionId>();
    } // For EF Core

    private KnowledgeQuery(
        KnowledgeQueryId id,
        string queryText,
        KnowledgeRetrievalStrategy strategy,
        Guid? conversationId,
        Guid? workflowId,
        IReadOnlyList<KnowledgeCollectionId> collectionScope)
    {
        Id = id;
        QueryText = queryText;
        Strategy = strategy;
        ConversationId = conversationId;
        WorkflowId = workflowId;
        CollectionScope = collectionScope;
    }

    public static KnowledgeQuery Create(
        string queryText,
        KnowledgeRetrievalStrategy strategy,
        Guid? conversationId = null,
        Guid? workflowId = null,
        IEnumerable<KnowledgeCollectionId>? collectionScope = null)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            throw new ArgumentException("Query text cannot be empty.", nameof(queryText));

        return new KnowledgeQuery(
            KnowledgeQueryId.CreateUnique(),
            queryText,
            strategy ?? throw new ArgumentNullException(nameof(strategy)),
            conversationId,
            workflowId,
            (collectionScope ?? Enumerable.Empty<KnowledgeCollectionId>()).ToList().AsReadOnly());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
        yield return QueryText;
        yield return Strategy;
        yield return ConversationId ?? Guid.Empty;
        yield return WorkflowId ?? Guid.Empty;
    }
}

/// <summary>
/// Ranking information attached to a retrieved result: relevance score, rank
/// position, and the signal that produced the score. Rankings are comparable
/// so packages can be ordered deterministically.
/// </summary>
public class KnowledgeRanking : ValueObject, IComparable<KnowledgeRanking>
{
    public double RelevanceScore { get; private set; }
    public int RankPosition { get; private set; }
    public string RankingSignal { get; private set; }

    private KnowledgeRanking() { RankingSignal = null!; } // For EF Core

    private KnowledgeRanking(double relevanceScore, int rankPosition, string rankingSignal)
    {
        RelevanceScore = relevanceScore;
        RankPosition = rankPosition;
        RankingSignal = rankingSignal;
    }

    public static KnowledgeRanking Create(double relevanceScore, int rankPosition, string rankingSignal = "relevance")
    {
        if (relevanceScore is < 0.0 or > 1.0)
            throw new ArgumentException("Relevance score must be between 0 and 1.", nameof(relevanceScore));
        if (rankPosition < 1)
            throw new ArgumentException("Rank position must be 1 or greater.", nameof(rankPosition));
        return new KnowledgeRanking(relevanceScore, rankPosition, rankingSignal ?? "relevance");
    }

    public int CompareTo(KnowledgeRanking? other)
    {
        if (other is null) return -1;
        // Higher relevance first; ties broken by rank position.
        var byScore = other.RelevanceScore.CompareTo(RelevanceScore);
        return byScore != 0 ? byScore : RankPosition.CompareTo(other.RankPosition);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RelevanceScore;
        yield return RankPosition;
        yield return RankingSignal;
    }
}

/// <summary>
/// A single retrieved knowledge item: the chunk content, its citation, ranking,
/// confidence, and a token estimate used for prompt budgeting.
/// </summary>
public class KnowledgeResult : ValueObject
{
    public KnowledgeChunkId ChunkId { get; private set; }
    public string Content { get; private set; }
    public KnowledgeCitation Citation { get; private set; }
    public KnowledgeRanking Ranking { get; private set; }
    public double Confidence { get; private set; }
    public int EstimatedTokens { get; private set; }

    private KnowledgeResult() { ChunkId = null!; Content = null!; Citation = null!; Ranking = null!; } // For EF Core

    private KnowledgeResult(
        KnowledgeChunkId chunkId,
        string content,
        KnowledgeCitation citation,
        KnowledgeRanking ranking,
        double confidence,
        int estimatedTokens)
    {
        ChunkId = chunkId;
        Content = content;
        Citation = citation;
        Ranking = ranking;
        Confidence = confidence;
        EstimatedTokens = estimatedTokens;
    }

    public static KnowledgeResult Create(
        KnowledgeChunkId chunkId,
        string content,
        KnowledgeCitation citation,
        KnowledgeRanking ranking,
        double confidence)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Result content cannot be empty.", nameof(content));
        if (confidence is < 0.0 or > 1.0)
            throw new ArgumentException("Confidence must be between 0 and 1.", nameof(confidence));

        // Conservative token estimate: ~4 characters per token (provider-agnostic heuristic).
        var estimatedTokens = Math.Max(1, content.Length / 4);

        return new KnowledgeResult(chunkId, content, citation, ranking, confidence, estimatedTokens);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ChunkId;
        yield return Content;
        yield return Confidence;
    }
}
