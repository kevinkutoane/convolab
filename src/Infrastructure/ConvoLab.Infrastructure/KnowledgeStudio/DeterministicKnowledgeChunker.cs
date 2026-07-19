using ConvoLab.Application.KnowledgeStudio;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

public sealed class DeterministicKnowledgeChunker : IKnowledgeChunker
{
    private const int ChunkSize = 1100;
    private const int Overlap = 200;

    public IReadOnlyList<KnowledgeChunkState> Chunk(
        ExtractedKnowledgeDocument document,
        KnowledgeDocumentState source)
    {
        var chunks = new List<KnowledgeChunkState>();
        var sequence = 0;
        foreach (var section in document.Sections)
        {
            var text = section.Text.Trim();
            if (text.Length < 20) continue;

            var start = 0;
            while (start < text.Length)
            {
                var take = Math.Min(ChunkSize, text.Length - start);
                var value = text.Substring(start, take).Trim();
                if (value.Length >= 20)
                {
                    chunks.Add(new KnowledgeChunkState(
                        Guid.NewGuid(),
                        source.Id,
                        source.CollectionId,
                        ++sequence,
                        value,
                        section.PageNumber,
                        section.Heading,
                        value.Length,
                        Math.Max(1, (int)Math.Ceiling(value.Length / 4d)),
                        source.Classification,
                        false));
                }
                if (start + take >= text.Length) break;
                start += Math.Max(1, ChunkSize - Overlap);
            }
        }
        return chunks;
    }
}
