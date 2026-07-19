using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Infrastructure.KnowledgeStudio;
using Microsoft.Extensions.Configuration;

namespace ConvoLab.Infrastructure.IntegrationTests.KnowledgeStudio;

public sealed class KnowledgeInfrastructureTests
{
    [Fact]
    public void Chunker_Is_Deterministic_And_Preserves_Page_Metadata()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new KnowledgeDocumentState(
            Guid.NewGuid(), Guid.NewGuid(), "Policy", "policy.txt", "text/plain", 2000, "key",
            KnowledgeDocumentStage.Chunking, KnowledgeClassification.Internal, "Kevin", "Claims", [], 1,
            null, now, now, null, 1);
        var section = new ExtractedSection("Hail", 42, new string('A', 1600));
        var document = new ExtractedKnowledgeDocument("Policy", section.Text, [section], []);
        var chunker = new DeterministicKnowledgeChunker();

        var first = chunker.Chunk(document, source);
        var second = chunker.Chunk(document, source);

        Assert.Equal(first.Select(x => x.Text), second.Select(x => x.Text));
        Assert.All(first, chunk => Assert.Equal(42, chunk.PageNumber));
        Assert.True(first.Count >= 2);
    }

    [Fact]
    public void Keyword_Retriever_Ranks_Exact_Phrase_First()
    {
        var documentId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var chunks = new[]
        {
            new KnowledgeChunkState(Guid.NewGuid(), documentId, collectionId, 2, "General storm information.", null, "Storm", 26, 7, KnowledgeClassification.Internal, true),
            new KnowledgeChunkState(Guid.NewGuid(), documentId, collectionId, 1, "Hail damage is covered under comprehensive cover.", 42, "Hail", 50, 13, KnowledgeClassification.Internal, true)
        };
        var retriever = new KeywordKnowledgeRetriever();

        var result = retriever.Rank("hail damage", new Dictionary<Guid, string> { [documentId] = "Policy" }, chunks, 5, 0.01);

        Assert.NotEmpty(result);
        Assert.Equal(1, result[0].Chunk.Sequence);
        Assert.True(result[0].Confidence > result.Last().Confidence || result.Count == 1);
    }

    [Fact]
    public async Task Local_Storage_Prevents_Path_Traversal_And_Is_Writable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"convolab-storage-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Knowledge:StoragePath"] = root })
                .Build();
            var storage = new LocalKnowledgeDocumentStorage(configuration);

            Assert.True(await storage.ProbeAsync());
            await using var input = new MemoryStream("hello"u8.ToArray());
            var stored = await storage.StoreAsync("../unsafe.txt", "text/plain", input);
            Assert.DoesNotContain("..", stored.StorageKey, StringComparison.Ordinal);
            Assert.True(await storage.ExistsAsync(stored.StorageKey));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
