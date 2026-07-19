using ConvoLab.Application.KnowledgeStudio;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

public sealed class KeywordKnowledgeRetriever : IKeywordKnowledgeRetriever
{
    public IReadOnlyList<RankedKnowledgeChunk> Rank(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence)
    {
        var terms = Tokenize(query);
        var normalizedQuery = query.Trim().ToLowerInvariant();

        return chunks
            .Select(chunk =>
            {
                var text = chunk.Text.ToLowerInvariant();
                var heading = (chunk.Section ?? string.Empty).ToLowerInvariant();
                var matching = terms.Where(term => text.Contains(term, StringComparison.Ordinal)).Distinct().ToList();
                var exactPhrase = text.Contains(normalizedQuery, StringComparison.Ordinal) ? 0.45 : 0;
                var termCoverage = matching.Count / (double)Math.Max(1, terms.Count) * 0.4;
                var headingBoost = matching.Any(term => heading.Contains(term, StringComparison.Ordinal)) ? 0.1 : 0;
                var earlyChunkBoost = chunk.Sequence <= 3 ? 0.05 : 0;
                var confidence = Math.Min(1, exactPhrase + termCoverage + headingBoost + earlyChunkBoost);
                return new RankedKnowledgeChunk(
                    chunk,
                    documentTitles.GetValueOrDefault(chunk.DocumentId, "Unknown document"),
                    confidence,
                    matching);
            })
            .Where(candidate => candidate.MatchingTerms.Count > 0 && candidate.Confidence >= minimumConfidence)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Chunk.Sequence)
            .Take(maxResults)
            .ToList();
    }

    private static IReadOnlyList<string> Tokenize(string query)
        => query.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', '?', '!', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 2)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
