using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Application.KnowledgeStudio;

/// <summary>
/// Enterprise hybrid knowledge retriever supporting Reciprocal Rank Fusion (RRF)
/// between lexical (keyword) and semantic (dense) search strategies.
/// </summary>
public interface IHybridKnowledgeRetriever : IKeywordKnowledgeRetriever
{
    IReadOnlyList<RankedKnowledgeChunk> RankHybrid(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence,
        RetrievalStrategyType strategy = RetrievalStrategyType.Hybrid);
}
