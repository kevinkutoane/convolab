using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Application.Tests.KnowledgeStudio;

public sealed class KnowledgeStudioServiceTests
{
    [Fact]
    public async Task Confidential_Document_Cannot_Publish_Without_Approval()
    {
        var repository = new InMemoryKnowledgeRepository();
        var service = CreateService(repository);
        var collection = await service.CreateCollectionAsync(new(
            "Claims", "", "Kevin", KnowledgeClassification.Confidential));
        var document = repository.AddProcessedDocument(collection.Id, KnowledgeClassification.Confidential);

        var error = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            service.TransitionAsync(document.Id, "publish", new("Kevin", "test")));
        Assert.Equal("knowledge.lifecycle.invalid_transition", error.Code);
    }

    [Fact]
    public async Task Retrieval_Respects_Token_Budget()
    {
        var repository = new InMemoryKnowledgeRepository();
        var service = CreateService(repository);
        var collection = await service.CreateCollectionAsync(new(
            "Claims", "", "Kevin", KnowledgeClassification.Internal));
        repository.AddPublishedDocumentWithChunk(collection.Id, "Hail policy", "Hail damage is covered under comprehensive cover.", 50);

        var result = await service.QueryAsync(collection.Id, new("hail damage", 5, 0.01, 10));
        Assert.Empty(result.Results);
        Assert.Contains(result.Exclusions, value => value.Contains("token budget", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task Stale_Knowledge_Lifecycle_Transition_Is_Rejected()
    {
        var repository = new InMemoryKnowledgeRepository();
        var service = CreateService(repository);
        var collection = await service.CreateCollectionAsync(new(
            "Claims", "", "Kevin", KnowledgeClassification.Internal));
        var document = repository.AddProcessedDocument(collection.Id, KnowledgeClassification.Internal);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.TransitionAsync(
                document.Id,
                "submit",
                new KnowledgeLifecycleCommand("Kevin", "test", document.Revision + 10)));
    }

    private static KnowledgeStudioService CreateService(InMemoryKnowledgeRepository repository)
        => new(repository, new FakeStorage(), new FakeResolver(), new FakeChunker(), new FakeRetriever(), new FakeUnitOfWork());

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
    private sealed class FakeStorage : IKnowledgeDocumentStorage
    {
        public Task DeleteAsync(string storageKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task<bool> ProbeAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<StoredKnowledgeDocument> StoreAsync(string fileName, string contentType, Stream content, CancellationToken ct = default)
            => Task.FromResult(new StoredKnowledgeDocument("key", fileName, contentType, content.Length));
    }
    private sealed class FakeResolver : IDocumentTextExtractorResolver
    {
        public IDocumentTextExtractor Resolve(string extension, string contentType) => new FakeExtractor();
    }
    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        public bool CanExtract(string extension, string contentType) => true;
        public Task<ExtractedKnowledgeDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
            => Task.FromResult(new ExtractedKnowledgeDocument("doc", "text", [new(null, null, "text")], []));
    }
    private sealed class FakeChunker : IKnowledgeChunker
    {
        public IReadOnlyList<KnowledgeChunkState> Chunk(ExtractedKnowledgeDocument document, KnowledgeDocumentState source) => [];
    }
    private sealed class FakeRetriever : IKeywordKnowledgeRetriever
    {
        public IReadOnlyList<RankedKnowledgeChunk> Rank(string query, IReadOnlyDictionary<Guid, string> titles, IReadOnlyList<KnowledgeChunkState> chunks, int maxResults, double minimumConfidence)
            => chunks.Select(chunk => new RankedKnowledgeChunk(chunk, titles[chunk.DocumentId], 0.9, ["hail"])).ToList();
    }

    private sealed class InMemoryKnowledgeRepository : IKnowledgeStudioRepository
    {
        private readonly Dictionary<Guid, KnowledgeCollectionState> _collections = [];
        private readonly Dictionary<Guid, KnowledgeDocumentState> _documents = [];
        private readonly List<KnowledgeChunkState> _chunks = [];

        public KnowledgeDocumentState AddProcessedDocument(Guid collectionId, KnowledgeClassification classification)
        {
            var now = DateTimeOffset.UtcNow;
            var document = new KnowledgeDocumentState(Guid.NewGuid(), collectionId, "Doc", "doc.txt", "text/plain", 1, "key", KnowledgeDocumentStage.Processed, classification, "Kevin", "General", [], 1, null, now, now, null, 1);
            _documents.Add(document.Id, document);
            return document;
        }
        public void AddPublishedDocumentWithChunk(Guid collectionId, string title, string text, int tokens)
        {
            var now = DateTimeOffset.UtcNow;
            var document = new KnowledgeDocumentState(Guid.NewGuid(), collectionId, title, "doc.txt", "text/plain", 1, "key", KnowledgeDocumentStage.Published, KnowledgeClassification.Internal, "Kevin", "General", [], 1, null, now, now, now, 1);
            _documents.Add(document.Id, document);
            _chunks.Add(new KnowledgeChunkState(Guid.NewGuid(), document.Id, collectionId, 1, text, null, "Section", text.Length, tokens, KnowledgeClassification.Internal, true));
        }

        public Task<IReadOnlyList<KnowledgeCollectionState>> ListCollectionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<KnowledgeCollectionState>>(_collections.Values.ToList());
        public Task<KnowledgeCollectionState?> GetCollectionAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_collections.GetValueOrDefault(id));
        public Task AddCollectionAsync(KnowledgeCollectionState collection, CancellationToken ct = default) { _collections.Add(collection.Id, collection); return Task.CompletedTask; }
        public Task UpdateCollectionAsync(KnowledgeCollectionState collection, long expectedRevision, CancellationToken ct = default) { if (_collections[collection.Id].Revision != expectedRevision) throw new ConcurrencyConflictException("knowledge collection", collection.Id); _collections[collection.Id] = collection; return Task.CompletedTask; }
        public Task<IReadOnlyList<KnowledgeDocumentState>> ListDocumentsAsync(Guid collectionId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<KnowledgeDocumentState>>(_documents.Values.Where(x => x.CollectionId == collectionId).ToList());
        public Task<KnowledgeDocumentState?> GetDocumentAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_documents.GetValueOrDefault(id));
        public Task AddDocumentAsync(KnowledgeDocumentState document, CancellationToken ct = default) { _documents.Add(document.Id, document); return Task.CompletedTask; }
        public Task UpdateDocumentAsync(KnowledgeDocumentState document, long expectedRevision, CancellationToken ct = default) { if (_documents[document.Id].Revision != expectedRevision) throw new ConcurrencyConflictException("knowledge document", document.Id); _documents[document.Id] = document; return Task.CompletedTask; }
        public Task DeleteDocumentAsync(Guid id, CancellationToken ct = default) { _documents.Remove(id); return Task.CompletedTask; }
        public Task<IReadOnlyList<KnowledgeChunkState>> ListChunksAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<KnowledgeChunkState>>(_chunks.Where(x => x.DocumentId == documentId).ToList());
        public Task<IReadOnlyList<KnowledgeChunkState>> ListCollectionChunksAsync(Guid collectionId, bool publishedOnly, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<KnowledgeChunkState>>(_chunks.Where(x => x.CollectionId == collectionId && (!publishedOnly || x.Published)).ToList());
        public Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<KnowledgeChunkState> chunks, CancellationToken ct = default) { _chunks.RemoveAll(x => x.DocumentId == documentId); _chunks.AddRange(chunks); return Task.CompletedTask; }
        public Task SetChunksPublishedAsync(Guid documentId, bool published, CancellationToken ct = default) { for (var i = 0; i < _chunks.Count; i++) if (_chunks[i].DocumentId == documentId) _chunks[i] = _chunks[i] with { Published = published }; return Task.CompletedTask; }
        public Task DeleteChunksAsync(Guid documentId, CancellationToken ct = default) { _chunks.RemoveAll(x => x.DocumentId == documentId); return Task.CompletedTask; }
        public Task AddLifecycleEntryAsync(KnowledgeLifecycleState entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteLifecycleAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
