using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.AI.ValueObjects;

public class AIProvider : ValueObject
{
    public string Name { get; private set; }
    public string? BaseUrl { get; private set; }

    private AIProvider(string name, string? baseUrl = null)
    {
        Name = name;
        BaseUrl = baseUrl;
    }

    public static AIProvider Create(string name, string? baseUrl = null) => new(name, baseUrl);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return BaseUrl ?? string.Empty;
    }

    private AIProvider() { Name = null!; }
}

public class AICompletion : ValueObject
{
    public string Content { get; private set; }
    public TokenUsage Usage { get; private set; }
    public string? FinishReason { get; private set; }

    private AICompletion(string content, TokenUsage usage, string? finishReason = null)
    {
        Content = content;
        Usage = usage;
        FinishReason = finishReason;
    }

    public static AICompletion Create(string content, TokenUsage usage, string? finishReason = null) => 
        new(content, usage, finishReason);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Content;
        yield return Usage;
        yield return FinishReason ?? string.Empty;
    }

    private AICompletion() { Content = null!; Usage = null!; }
}

public class TokenUsage : ValueObject
{
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public int TotalTokens => PromptTokens + CompletionTokens;

    private TokenUsage(int promptTokens, int completionTokens)
    {
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
    }

    public static TokenUsage Create(int promptTokens, int completionTokens) => 
        new(promptTokens, completionTokens);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PromptTokens;
        yield return CompletionTokens;
    }

    private TokenUsage() { }
}

public class AICost : ValueObject
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    private AICost(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static AICost Create(decimal amount, string currency = "USD") => 
        new(amount, currency);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private AICost() { Currency = "USD"; }
}

public class AIEmbedding : ValueObject
{
    public IReadOnlyList<float> Vector { get; private set; }
    public int Dimensions => Vector.Count;

    private AIEmbedding(IReadOnlyList<float> vector)
    {
        Vector = vector;
    }

    public static AIEmbedding Create(IReadOnlyList<float> vector) => new(vector);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var value in Vector)
        {
            yield return value;
        }
    }

    private AIEmbedding() { Vector = new List<float>(); }
}
