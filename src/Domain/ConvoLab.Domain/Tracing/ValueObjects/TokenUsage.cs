using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Tracing.ValueObjects;

public class TokenUsage : ValueObject
{
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public int TotalTokens { get; private set; }

    public TokenUsage(int promptTokens, int completionTokens, int totalTokens)
    {
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PromptTokens;
        yield return CompletionTokens;
        yield return TotalTokens;
    }

    // For EF Core
    private TokenUsage() { }
}
