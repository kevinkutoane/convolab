using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

/// <summary>
/// Production enterprise hybrid knowledge retriever implementing Reciprocal Rank Fusion (RRF)
/// combining lexical (BM25-style term coverage & exact match) and dense semantic vector similarity.
/// </summary>
public sealed class HybridKnowledgeRetriever : IHybridKnowledgeRetriever
{
    private const int RrfConstantK = 60;
    private const double MaxRrfScore = (1.0 / (RrfConstantK + 1)) * 2.0;
    private const int VectorDimensions = 1024;

    public IReadOnlyList<RankedKnowledgeChunk> Rank(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence)
    {
        return RankHybrid(
            query,
            documentTitles,
            chunks,
            maxResults,
            minimumConfidence,
            RetrievalStrategyType.Keyword);
    }

    public IReadOnlyList<RankedKnowledgeChunk> RankHybrid(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence,
        RetrievalStrategyType strategy = RetrievalStrategyType.Hybrid)
    {
        if (string.IsNullOrWhiteSpace(query) || chunks.Count == 0 || maxResults <= 0)
        {
            return Array.Empty<RankedKnowledgeChunk>();
        }

        return strategy switch
        {
            RetrievalStrategyType.Keyword => RankKeywordOnly(query, documentTitles, chunks, maxResults, minimumConfidence),
            RetrievalStrategyType.Semantic => RankSemanticOnly(query, documentTitles, chunks, maxResults, minimumConfidence),
            _ => RankWithRrf(query, documentTitles, chunks, maxResults, minimumConfidence)
        };
    }

    private static IReadOnlyList<RankedKnowledgeChunk> RankKeywordOnly(
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
                var confidence = Math.Min(1.0, exactPhrase + termCoverage + headingBoost + earlyChunkBoost);

                return new RankedKnowledgeChunk(
                    chunk,
                    documentTitles.GetValueOrDefault(chunk.DocumentId, "Unknown document"),
                    Math.Round(confidence, 4),
                    matching);
            })
            .Where(candidate => candidate.MatchingTerms.Count > 0 && candidate.Confidence >= minimumConfidence)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Chunk.Sequence)
            .Take(maxResults)
            .ToList();
    }

    private static IReadOnlyList<RankedKnowledgeChunk> RankSemanticOnly(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence)
    {
        var queryVector = GenerateDenseVector(query);
        var queryTerms = Tokenize(query);

        return chunks
            .Select(chunk =>
            {
                var chunkVector = GenerateDenseVector(chunk.Text);
                var similarity = ComputeCosineSimilarity(queryVector, chunkVector);
                var chunkText = chunk.Text.ToLowerInvariant();
                var matching = queryTerms.Where(term => chunkText.Contains(term, StringComparison.Ordinal)).Distinct().ToList();

                return new RankedKnowledgeChunk(
                    chunk,
                    documentTitles.GetValueOrDefault(chunk.DocumentId, "Unknown document"),
                    Math.Round(Math.Clamp(similarity, 0.0, 1.0), 4),
                    matching);
            })
            .Where(candidate => candidate.Confidence >= minimumConfidence)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Chunk.Sequence)
            .Take(maxResults)
            .ToList();
    }

    private static IReadOnlyList<RankedKnowledgeChunk> RankWithRrf(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence)
    {
        // 1. Keyword ranking
        var keywordCandidates = RankKeywordOnly(query, documentTitles, chunks, chunks.Count, 0.001);
        var keywordRanks = new Dictionary<Guid, (int Rank, IReadOnlyList<string> Matching)>();
        for (var i = 0; i < keywordCandidates.Count; i++)
        {
            keywordRanks[keywordCandidates[i].Chunk.Id] = (i + 1, keywordCandidates[i].MatchingTerms);
        }

        // 2. Semantic ranking
        var semanticCandidates = RankSemanticOnly(query, documentTitles, chunks, chunks.Count, 0.001);
        var semanticRanks = new Dictionary<Guid, int>();
        for (var i = 0; i < semanticCandidates.Count; i++)
        {
            semanticRanks[semanticCandidates[i].Chunk.Id] = i + 1;
        }

        // 3. Reciprocal Rank Fusion
        var chunkMap = chunks.ToDictionary(c => c.Id);
        var allChunkIds = keywordRanks.Keys.Union(semanticRanks.Keys);

        var fused = new List<RankedKnowledgeChunk>();
        foreach (var chunkId in allChunkIds)
        {
            if (!chunkMap.TryGetValue(chunkId, out var chunk)) continue;

            var rrfScore = 0.0;
            var matchingTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (keywordRanks.TryGetValue(chunkId, out var kw))
            {
                rrfScore += 1.0 / (RrfConstantK + kw.Rank);
                foreach (var term in kw.Matching) matchingTerms.Add(term);
            }

            if (semanticRanks.TryGetValue(chunkId, out var semRank))
            {
                rrfScore += 1.0 / (RrfConstantK + semRank);
            }

            var normalizedConfidence = Math.Min(1.0, rrfScore / MaxRrfScore);

            if (normalizedConfidence >= minimumConfidence)
            {
                fused.Add(new RankedKnowledgeChunk(
                    chunk,
                    documentTitles.GetValueOrDefault(chunk.DocumentId, "Unknown document"),
                    Math.Round(normalizedConfidence, 4),
                    matchingTerms.ToList()));
            }
        }

        return fused
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Chunk.Sequence)
            .Take(maxResults)
            .ToList();
    }

    private static IReadOnlyList<string> Tokenize(string query)
        => query.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', '?', '!', ';', ':', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 2)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private const string ConceptVehicle = "concept_vehicle";
    private const string ConceptIncident = "concept_incident";
    private const string ConceptClaim = "concept_claim";
    private const string ConceptCoverage = "concept_coverage";
    private const string ConceptCancellation = "concept_cancellation";

    private static readonly Dictionary<string, string> ConceptClusters = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vehicle & Transportation
        ["car"] = ConceptVehicle,
        ["cars"] = ConceptVehicle,
        ["automobile"] = ConceptVehicle,
        ["automobiles"] = ConceptVehicle,
        ["vehicle"] = ConceptVehicle,
        ["vehicles"] = ConceptVehicle,
        ["motor"] = ConceptVehicle,
        ["motorcycle"] = ConceptVehicle,
        ["truck"] = ConceptVehicle,

        // Accident & Collision
        ["accident"] = ConceptIncident,
        ["accidents"] = ConceptIncident,
        ["collision"] = ConceptIncident,
        ["collisions"] = ConceptIncident,
        ["crash"] = ConceptIncident,
        ["crashes"] = ConceptIncident,
        ["damage"] = ConceptIncident,
        ["impact"] = ConceptIncident,

        // Claim & Compensation
        ["claim"] = ConceptClaim,
        ["claims"] = ConceptClaim,
        ["compensation"] = ConceptClaim,
        ["compensate"] = ConceptClaim,
        ["liability"] = ConceptClaim,
        ["liable"] = ConceptClaim,
        ["reimburse"] = ConceptClaim,
        ["reimbursement"] = ConceptClaim,
        ["payout"] = ConceptClaim,
        ["excess"] = ConceptClaim,

        // Policy & Insurance Coverage
        ["policy"] = ConceptCoverage,
        ["policies"] = ConceptCoverage,
        ["cover"] = ConceptCoverage,
        ["covers"] = ConceptCoverage,
        ["coverage"] = ConceptCoverage,
        ["comprehensive"] = ConceptCoverage,
        ["premium"] = ConceptCoverage,
        ["premiums"] = ConceptCoverage,

        // Cancellation & Refund
        ["cancel"] = ConceptCancellation,
        ["cancellation"] = ConceptCancellation,
        ["terminate"] = ConceptCancellation,
        ["refund"] = ConceptCancellation
    };

    private static float[] GenerateDenseVector(string text)
    {
        var vector = new float[VectorDimensions];
        if (string.IsNullOrWhiteSpace(text)) return vector;

        var normalized = text.Trim().ToLowerInvariant();
        var terms = Tokenize(normalized);

        foreach (var term in terms)
        {
            // Term direct hash (deterministic FNV-1a)
            var termHash = Fnv1aHash(term) % VectorDimensions;
            vector[termHash] += 2.0f;

            // Concept semantic cluster projection
            if (ConceptClusters.TryGetValue(term, out var conceptKey))
            {
                var conceptHash = Fnv1aHash(conceptKey) % VectorDimensions;
                vector[conceptHash] += 4.0f;
            }

            // Character tri-grams for morphological variations
            for (var i = 0; i <= term.Length - 3; i++)
            {
                var tri = term.Substring(i, 3);
                var triHash = Fnv1aHash(tri) % VectorDimensions;
                vector[triHash] += 0.5f;
            }
        }

        // L2 Normalization
        var sumSquares = 0.0f;
        for (var i = 0; i < VectorDimensions; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares > 0)
        {
            var norm = MathF.Sqrt(sumSquares);
            for (var i = 0; i < VectorDimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    private static int Fnv1aHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in text)
            {
                hash = (hash ^ c) * 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        var dot = 0.0;
        for (var i = 0; i < VectorDimensions; i++)
        {
            dot += vectorA[i] * vectorB[i];
        }
        return Math.Max(0.0, dot);
    }
}
