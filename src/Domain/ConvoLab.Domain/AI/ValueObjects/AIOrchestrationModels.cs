using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.AI.ValueObjects;

public class AIProvider : ValueObject
{
    public string Name { get; private set; }
    public string? BaseUrl { get; private set; }
    public bool IsActive { get; private set; }
    public ProviderHealth Health { get; private set; }

    private AIProvider(string name, string? baseUrl = null, bool isActive = true)
    {
        Name = name;
        BaseUrl = baseUrl;
        IsActive = isActive;
        Health = ProviderHealth.Healthy;
    }

    public static AIProvider Create(string name, string? baseUrl = null) => new(name, baseUrl);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return BaseUrl ?? string.Empty;
    }

    private AIProvider() { Name = null!; }
}

public enum ProviderHealth
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

public class AIModel : ValueObject
{
    public AIModelId Id { get; private set; }
    public string Name { get; private set; }
    public string ProviderName { get; private set; }
    public ModelCapability Capabilities { get; private set; }
    public ModelAvailability Availability { get; private set; }

    public AIModel(AIModelId id, string name, string providerName, ModelCapability capabilities)
    {
        Id = id;
        Name = name;
        ProviderName = providerName;
        Capabilities = capabilities;
        Availability = ModelAvailability.Available;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
        yield return ProviderName;
    }

    private AIModel() { Id = null!; Name = null!; ProviderName = null!; }
}

[Flags]
public enum ModelCapability
{
    None = 0,
    Completion = 1,
    Chat = 2,
    Embedding = 4,
    ImageGeneration = 8,
    ToolCalling = 16,
    Streaming = 32,
    Vision = 64
}

public enum ModelAvailability
{
    Available,
    Deprecated,
    Retired,
    Unavailable
}

public class CompletionRequest : ValueObject
{
    public AIModelId ModelId { get; private set; }
    public string Prompt { get; private set; }
    public IReadOnlyList<AIMessage> Messages { get; private set; }
    public float Temperature { get; private set; }
    public int MaxTokens { get; private set; }

    private CompletionRequest(AIModelId modelId, string prompt, IEnumerable<AIMessage> messages, float temperature, int maxTokens)
    {
        ModelId = modelId;
        Prompt = prompt;
        Messages = messages.ToList().AsReadOnly();
        Temperature = temperature;
        MaxTokens = maxTokens;
    }

    public static CompletionRequest Create(AIModelId modelId, string prompt, IEnumerable<AIMessage> messages, float temperature = 0.7f, int maxTokens = 2048) =>
        new(modelId, prompt, messages, temperature, maxTokens);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ModelId;
        yield return Prompt;
        foreach (var message in Messages)
        {
            yield return message;
        }
        yield return Temperature;
        yield return MaxTokens;
    }

    private CompletionRequest() { ModelId = null!; Prompt = null!; Messages = new List<AIMessage>().AsReadOnly(); }
}
public class AIMessage : ValueObject
{
    public string Role { get; private set; }
    public string Content { get; private set; }

    private AIMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    public static AIMessage Create(string role, string content) => new(role, content);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Role;
        yield return Content;
    }

    private AIMessage() { Role = null!; Content = null!; }
}

public class AICompletion : ValueObject
{
    public string Content { get; private set; }
    public TokenUsage Usage { get; private set; }
    public string? FinishReason { get; private set; }
    public AICost? Cost { get; private set; }

    private AICompletion(string content, TokenUsage usage, string? finishReason = null, AICost? cost = null)
    {
        Content = content;
        Usage = usage;
        FinishReason = finishReason;
        Cost = cost;
    }

    public static AICompletion Create(string content, TokenUsage usage, string? finishReason = null, AICost? cost = null) => 
        new(content, usage, finishReason, cost);

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
    public TokenUsage? Usage { get; private set; }

    private AIEmbedding(IReadOnlyList<float> vector, TokenUsage? usage = null)
    {
        Vector = vector;
        Usage = usage;
    }

    public static AIEmbedding Create(IReadOnlyList<float> vector, TokenUsage? usage = null) => new(vector, usage);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var value in Vector)
        {
            yield return value;
        }
    }

    private AIEmbedding() { Vector = new List<float>(); }
}

public class EmbeddingRequest : ValueObject
{
    public AIModelId ModelId { get; private set; }
    public string Input { get; private set; }

    private EmbeddingRequest(AIModelId modelId, string input)
    {
        ModelId = modelId;
        Input = input;
    }

    public static EmbeddingRequest Create(AIModelId modelId, string input) => new(modelId, input);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ModelId;
        yield return Input;
    }

    private EmbeddingRequest() { ModelId = null!; Input = null!; }
}
