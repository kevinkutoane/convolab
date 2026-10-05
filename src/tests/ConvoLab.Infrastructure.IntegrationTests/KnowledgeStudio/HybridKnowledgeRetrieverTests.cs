using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Infrastructure.KnowledgeStudio;

namespace ConvoLab.Infrastructure.IntegrationTests.KnowledgeStudio;

public sealed class HybridKnowledgeRetrieverTests
{
    private readonly HybridKnowledgeRetriever _retriever = new();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _collectionId = Guid.NewGuid();

    [Fact]
    public void Keyword_Strategy_Prioritizes_Exact_Matches()
    {
        var chunks = new[]
        {
            CreateChunk(1, "Comprehensive motor insurance covers accident and storm damage."),
            CreateChunk(2, "Third-party motor insurance covers only damage to external vehicles."),
            CreateChunk(3, "Pet insurance covers veterinary expenses and surgeries.")
        };
        var titles = new Dictionary<Guid, string> { [_documentId] = "Motor Policy" };

        var results = _retriever.RankHybrid(
            "comprehensive motor insurance",
            titles,
            chunks,
            maxResults: 5,
            minimumConfidence: 0.01,
            strategy: RetrievalStrategyType.Keyword);

        Assert.NotEmpty(results);
        Assert.Equal(1, results[0].Chunk.Sequence);
        Assert.Contains("comprehensive", results[0].MatchingTerms);
        Assert.Contains("motor", results[0].MatchingTerms);
    }

    [Fact]
    public void Semantic_Strategy_Identifies_Conceptually_Relevant_Chunks()
    {
        var chunks = new[]
        {
            CreateChunk(1, "Automobile accident compensation and vehicle repair liability coverage."),
            CreateChunk(2, "Home burglary and household contents protection guidelines."),
            CreateChunk(3, "Life insurance premium schedules and mortality benefits table.")
        };
        var titles = new Dictionary<Guid, string> { [_documentId] = "Auto Guide" };

        var results = _retriever.RankHybrid(
            "car collision claims",
            titles,
            chunks,
            maxResults: 5,
            minimumConfidence: 0.001,
            strategy: RetrievalStrategyType.Semantic);

        Assert.NotEmpty(results);
        // Chunk 1 has highest conceptual similarity (automobile/accident/vehicle/repair/liability)
        Assert.Equal(1, results[0].Chunk.Sequence);
        Assert.True(results[0].Confidence > 0);
    }

    [Fact]
    public void Hybrid_Strategy_Applies_Reciprocal_Rank_Fusion_Successfully()
    {
        var chunks = new[]
        {
            CreateChunk(1, "Vehicle collision coverage pays for repairs after a road accident."),
            CreateChunk(2, "Health insurance hospital cash back benefits and emergency treatment."),
            CreateChunk(3, "Accident reporting deadline is within twenty four hours of collision.")
        };
        var titles = new Dictionary<Guid, string> { [_documentId] = "Claims Guide" };

        var results = _retriever.RankHybrid(
            "accident collision reporting",
            titles,
            chunks,
            maxResults: 5,
            minimumConfidence: 0.01,
            strategy: RetrievalStrategyType.Hybrid);

        Assert.NotEmpty(results);
        // Both chunk 1 and 3 are relevant, chunk 3 has exact matches for accident/collision/reporting
        Assert.Contains(results, r => r.Chunk.Sequence == 3);
        Assert.Contains(results, r => r.Chunk.Sequence == 1);
        Assert.True(results[0].Confidence > 0.5);
    }

    [Fact]
    public void Retriever_Handles_Empty_Queries_And_Empty_Chunks_Gracefully()
    {
        var titles = new Dictionary<Guid, string> { [_documentId] = "Policy" };

        var emptyResults1 = _retriever.RankHybrid("", titles, [], 5, 0.05, RetrievalStrategyType.Hybrid);
        var emptyResults2 = _retriever.RankHybrid("hello", titles, [], 5, 0.05, RetrievalStrategyType.Hybrid);
        var emptyResults3 = _retriever.RankHybrid("   ", titles, [CreateChunk(1, "Sample text")], 5, 0.05, RetrievalStrategyType.Hybrid);

        Assert.Empty(emptyResults1);
        Assert.Empty(emptyResults2);
        Assert.Empty(emptyResults3);
    }

    [Fact]
    public void Backward_Compatible_IKeywordKnowledgeRetriever_Rank_Works()
    {
        var chunks = new[]
        {
            CreateChunk(1, "Windscreen chip repair is covered with zero excess payment required."),
            CreateChunk(2, "Travel cancellation insurance guidelines.")
        };
        var titles = new Dictionary<Guid, string> { [_documentId] = "Excess Policy" };

        // Calling through the standard IKeywordKnowledgeRetriever.Rank signature
        IKeywordKnowledgeRetriever retriever = _retriever;
        var results = retriever.Rank("windscreen chip", titles, chunks, 5, 0.01);

        Assert.NotEmpty(results);
        Assert.Equal(1, results[0].Chunk.Sequence);
    }

    private KnowledgeChunkState CreateChunk(int sequence, string text)
    {
        return new KnowledgeChunkState(
            Guid.NewGuid(),
            _documentId,
            _collectionId,
            sequence,
            text,
            PageNumber: 1,
            Section: "Section " + sequence,
            CharacterCount: text.Length,
            EstimatedTokens: text.Length / 4,
            Classification: KnowledgeClassification.Internal,
            Published: true);
    }
}
