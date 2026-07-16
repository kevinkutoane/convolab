using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Knowledge;

public class KnowledgePackageTests
{
    private readonly KnowledgeQuery _query;
    private readonly KnowledgeCitation _citation;

    public KnowledgePackageTests()
    {
        var strategy = KnowledgeRetrievalStrategy.Semantic(maxResults: 3, minConfidence: 0.7);
        _query = KnowledgeQuery.Create("How do I claim?", strategy);

        var reference = KnowledgeReference.Create(KnowledgeSourceId.CreateUnique(), "ext-1", "uri", "Doc 1");
        _citation = KnowledgeCitation.Create(reference, KnowledgeDocumentId.CreateUnique(), KnowledgeVersion.Initial());
    }

    [Fact]
    public void AddResult_Should_Reject_Below_Confidence_Threshold()
    {
        var package = KnowledgePackage.StartAssembly(_query);
        var lowConfidenceResult = KnowledgeResult.Create(
            KnowledgeChunkId.CreateUnique(), "content", _citation, KnowledgeRanking.Create(0.5, 1), confidence: 0.5);

        // Strategy requires 0.7
        Assert.Throws<InvalidOperationException>(() => package.AddResult(lowConfidenceResult));
    }

    [Fact]
    public void AddResult_Should_Reject_Exceeding_MaxResults()
    {
        var package = KnowledgePackage.StartAssembly(_query); // MaxResults = 3

        for (int i = 0; i < 3; i++)
        {
            var result = KnowledgeResult.Create(
                KnowledgeChunkId.CreateUnique(), "content", _citation, KnowledgeRanking.Create(0.9, i + 1), confidence: 0.9);
            package.AddResult(result);
        }

        var extraResult = KnowledgeResult.Create(
            KnowledgeChunkId.CreateUnique(), "content", _citation, KnowledgeRanking.Create(0.9, 4), confidence: 0.9);

        Assert.Throws<InvalidOperationException>(() => package.AddResult(extraResult));
    }

    [Fact]
    public void Seal_Should_Order_Results_By_Ranking_And_Raise_Events()
    {
        var package = KnowledgePackage.StartAssembly(_query);

        // Add out of order
        package.AddResult(KnowledgeResult.Create(
            KnowledgeChunkId.CreateUnique(), "content 2", _citation, KnowledgeRanking.Create(0.8, 2), confidence: 0.8));
        package.AddResult(KnowledgeResult.Create(
            KnowledgeChunkId.CreateUnique(), "content 1", _citation, KnowledgeRanking.Create(0.9, 1), confidence: 0.9));

        package.Seal();

        Assert.True(package.IsSealed);
        Assert.Equal(2, package.Results.Count);

        // Rank 1 should be first
        Assert.Equal(1, package.Results.First().Ranking.RankPosition);

        Assert.Contains(package.DomainEvents, e => e is KnowledgePackageCreatedEvent);
        Assert.Contains(package.DomainEvents, e => e is KnowledgeRetrievedEvent);

        // Cannot add after seal
        Assert.Throws<InvalidOperationException>(() => package.AddResult(
            KnowledgeResult.Create(KnowledgeChunkId.CreateUnique(), "content 3", _citation, KnowledgeRanking.Create(0.7, 3), confidence: 0.7)));
    }

    [Fact]
    public void FitToTokenBudget_Should_Trim_Results_To_Fit()
    {
        var package = KnowledgePackage.StartAssembly(_query);

        // Token heuristic: Length / 4. "12345678" -> 2 tokens.
        package.AddResult(KnowledgeResult.Create(
            KnowledgeChunkId.CreateUnique(), new string('a', 40), _citation, KnowledgeRanking.Create(0.9, 1), confidence: 0.9)); // ~10 tokens
        package.AddResult(KnowledgeResult.Create(
            KnowledgeChunkId.CreateUnique(), new string('b', 40), _citation, KnowledgeRanking.Create(0.8, 2), confidence: 0.8)); // ~10 tokens

        package.Seal();

        Assert.Equal(20, package.TotalEstimatedTokens);

        // Budget of 15 should only fit the first result
        var trimmed = package.FitToTokenBudget(15);

        Assert.Single(trimmed);
        Assert.Equal(1, trimmed.First().Ranking.RankPosition);
    }
}
