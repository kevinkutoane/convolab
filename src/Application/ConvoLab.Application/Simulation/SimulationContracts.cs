namespace ConvoLab.Application.Simulation;

public enum SimulationMode
{
    Normal,
    RetryOnce,
    Fallback
}

public sealed record CreateSimulationCommand(
    string Title,
    string Workflow,
    string PromptVersion,
    string KnowledgeCollection);

public sealed record SendSimulationMessageCommand(
    string Content,
    SimulationMode Mode = SimulationMode.Normal,
    string Provider = "Deterministic",
    string? Model = null,
    double Temperature = 0.2,
    int MaxOutputTokens = 400);

public sealed record ReplaySimulationCommand(
    Guid RunId,
    SimulationMode Mode = SimulationMode.Normal,
    string Provider = "Deterministic",
    string? Model = null,
    double Temperature = 0.2,
    int MaxOutputTokens = 400);

public sealed record SimulationSummary(
    Guid Id,
    string Title,
    string Status,
    string Workflow,
    string PromptVersion,
    string KnowledgeCollection,
    int MessageCount,
    int RunCount,
    string? LastMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SimulationConversation(
    Guid Id,
    string Title,
    string Status,
    string Workflow,
    string PromptVersion,
    string KnowledgeCollection,
    IReadOnlyList<SimulationMessage> Messages,
    IReadOnlyList<SimulationRun> Runs,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SimulationMessage(
    Guid Id,
    string Role,
    string Content,
    bool IsReplay,
    DateTimeOffset CreatedAt);

public sealed record SimulationRun(
    Guid Id,
    Guid UserMessageId,
    Guid? AssistantMessageId,
    Guid? ReplayedFromRunId,
    string Status,
    SimulationMode Mode,
    SimulationWorkflowSnapshot? Workflow,
    string RenderedPrompt,
    SimulationKnowledgePackage KnowledgePackage,
    SimulationExecutionPlan? ExecutionPlan,
    SimulationExecutionMetrics? Metrics,
    SimulationEvaluation Evaluation,
    IReadOnlyList<SimulationTimelineStep> Timeline,
    string? FailureReason,
    DateTimeOffset CreatedAt);


public sealed record SimulationWorkflowSnapshot(
    Guid? WorkflowId,
    Guid? VersionId,
    string Name,
    string Version,
    string Source,
    IReadOnlyList<SimulationWorkflowNode> Nodes,
    IReadOnlyList<SimulationWorkflowTransition> Transitions);

public sealed record SimulationWorkflowNode(
    Guid Id,
    string Name,
    string Kind,
    int Sequence);

public sealed record SimulationWorkflowTransition(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    string Label,
    string? Condition);

public sealed record SimulationKnowledgePackage(
    Guid Id,
    string Collection,
    string RetrievalStrategy,
    double Confidence,
    int TokenEstimate,
    IReadOnlyList<SimulationCitation> Citations);

public sealed record SimulationCitation(
    string Source,
    string Section,
    string Snippet);

public sealed record SimulationExecutionPlan(
    Guid Id,
    string Provider,
    string Model,
    bool Streaming,
    bool ToolsAllowed,
    int MaxAttempts,
    int FallbackCount,
    int EstimatedInputTokens,
    int EstimatedOutputTokens,
    decimal EstimatedCost,
    string Currency,
    double EstimatedLatencyMs,
    int Attempts,
    int FallbacksUsed);

public sealed record SimulationExecutionMetrics(
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    decimal ActualCost,
    string Currency,
    double TotalDurationMs,
    double ProviderLatencyMs);

public sealed record SimulationEvaluation(
    double Groundedness,
    double Relevance,
    double Safety,
    string Verdict);

public sealed record SimulationTimelineStep(
    Guid Id,
    string Name,
    string Capability,
    string Status,
    string Detail,
    DateTimeOffset StartedAt,
    double DurationMs);

public sealed record SimulationOptions(
    IReadOnlyList<string> Workflows,
    IReadOnlyList<string> PromptVersions,
    IReadOnlyList<string> KnowledgeCollections,
    IReadOnlyList<string> Modes,
    IReadOnlyList<SimulationProviderOption> Providers);

public sealed record SimulationProviderOption(
    string Key,
    string DisplayName,
    string DefaultModel,
    bool IsConfigured,
    bool IsLive,
    string Status,
    string? ConfigurationHint);
