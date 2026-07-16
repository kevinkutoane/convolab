using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Entities;

/// <summary>
/// A retrievable unit of knowledge produced by chunking a document. Chunks are
/// domain concepts only — no embedding computation happens here. A chunk may
/// carry an opaque reference to an embedding held in an external vector store.
/// </summary>
public class KnowledgeChunk : BaseEntity<KnowledgeChunkId>
{
    public KnowledgeDocumentId DocumentId { get; private set; }
    public ChunkType Type { get; private set; }
    public string Content { get; private set; }
    public int SequenceNumber { get; private set; }
    public int EstimatedTokens { get; private set; }
    public KnowledgeEmbeddingReference? EmbeddingReference { get; private set; }
    public KnowledgeMetadata Metadata { get; private set; }

    private KnowledgeChunk() { DocumentId = null!; Content = null!; Metadata = null!; } // For EF Core

    private KnowledgeChunk(
        KnowledgeChunkId id,
        KnowledgeDocumentId documentId,
        ChunkType type,
        string content,
        int sequenceNumber,
        KnowledgeMetadata metadata) : base(id)
    {
        DocumentId = documentId;
        Type = type;
        Content = content;
        SequenceNumber = sequenceNumber;
        Metadata = metadata;
        // Provider-agnostic heuristic: ~4 characters per token.
        EstimatedTokens = Math.Max(1, content.Length / 4);
    }

    public static KnowledgeChunk Create(
        KnowledgeDocumentId documentId,
        ChunkType type,
        string content,
        int sequenceNumber,
        KnowledgeMetadata? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Chunk content cannot be empty.", nameof(content));
        if (sequenceNumber < 0)
            throw new ArgumentException("Sequence number cannot be negative.", nameof(sequenceNumber));

        return new KnowledgeChunk(
            KnowledgeChunkId.CreateUnique(),
            documentId,
            type,
            content,
            sequenceNumber,
            metadata ?? KnowledgeMetadata.Empty());
    }

    /// <summary>
    /// Attaches an opaque embedding reference produced by an external indexing pipeline.
    /// The domain never stores vectors.
    /// </summary>
    public void AttachEmbeddingReference(KnowledgeEmbeddingReference reference)
    {
        EmbeddingReference = reference ?? throw new ArgumentNullException(nameof(reference));
        LastModifiedAt = DateTime.UtcNow;
    }

    public bool HasEmbedding => EmbeddingReference is not null;
}
