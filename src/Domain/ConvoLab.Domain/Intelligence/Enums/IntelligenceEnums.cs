namespace ConvoLab.Domain.Intelligence.Enums;

/// <summary>
/// Intelligent workload capabilities. Deliberately open-ended: adding a new
/// capability is a one-line change requiring no domain redesign.
/// </summary>
public enum IntelligenceCapability
{
    TextGeneration,
    Chat,
    Embedding,
    Vision,
    Ocr,
    SpeechRecognition,
    SpeechSynthesis,
    ToolCalling,
    FunctionCalling,
    StructuredOutput,
    JsonMode,
    Streaming,
    Reasoning,
    ImageGeneration,
    DocumentAnalysis,
    Classification,
    Summarisation,
    Planning
}

/// <summary>
/// Provider families modelled as business concepts, never SDKs. Custom covers
/// any future provider without domain change.
/// </summary>
public enum ProviderKind
{
    OpenAI,
    AzureOpenAI,
    Gemini,
    Anthropic,
    Mistral,
    Ollama,
    InternalModel,
    Custom
}

/// <summary>Lifecycle of an intelligent execution, from request to finish.</summary>
public enum ExecutionStatus
{
    Requested,
    Planned,
    Validated,
    Executing,
    Streaming,
    Completed,
    Evaluated,
    Recorded,
    Finished,
    Failed,
    Cancelled,
    TimedOut
}

/// <summary>Categories of tools the platform can invoke, provider-independent.</summary>
public enum ToolKind
{
    Internal,
    External,
    Workflow,
    Knowledge,
    Plugin,
    Rest,
    Mcp
}

/// <summary>Lifecycle of a single tool invocation.</summary>
public enum ToolInvocationStatus
{
    Requested,
    Executing,
    Completed,
    Failed,
    Rejected
}

/// <summary>Lifecycle of a streaming session.</summary>
public enum StreamingStatus
{
    Opened,
    Streaming,
    Completed,
    Aborted
}

/// <summary>Circuit breaker states guarding a provider.</summary>
public enum CircuitStatus
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>Provider operational availability.</summary>
public enum ProviderAvailability
{
    Available,
    Degraded,
    Unavailable,
    Maintenance,
    Unknown
}

/// <summary>Model lifecycle within the platform catalogue.</summary>
public enum ModelLifecycleStatus
{
    Registered,
    Active,
    Deprecated,
    Retired
}

/// <summary>Classes of execution failure, driving retry/fallback decisions.</summary>
public enum FailureKind
{
    Transient,
    RateLimited,
    Timeout,
    ProviderUnavailable,
    BudgetExceeded,
    PolicyViolation,
    SafetyRejection,
    InvalidRequest,
    Permanent
}

/// <summary>Safety pipeline stage outcomes.</summary>
public enum SafetyVerdict
{
    Approved,
    ApprovedWithWarnings,
    Rejected
}

/// <summary>Safety pipeline stages the engine coordinates.</summary>
public enum SafetyStage
{
    InputValidation,
    PromptPolicy,
    PiiDetection,
    ContentModeration,
    Compliance,
    OutputValidation
}

/// <summary>Kinds of artifacts an execution can produce beyond text.</summary>
public enum ArtifactKind
{
    Text,
    Json,
    Image,
    Audio,
    Embedding,
    File,
    ToolResult
}
