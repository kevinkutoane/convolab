using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IKnowledgeEngine
{
    Task<string> RetrieveContextAsync(string query, KnowledgeBaseId? knowledgeBaseId, ValueObjects.ExecutionContext context);
}
