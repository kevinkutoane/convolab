using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>
/// Everything the Execution Planner needs to decide HOW to run a workload:
/// origin (conversation/workflow), prompt metadata, knowledge provenance,
/// budget, latency target, required capabilities, and tenant.
/// The Intelligence Engine never sees raw conversations — only context.
/// </summary>
public class ExecutionContext : ValueObject
{
    public Guid? ConversationId { get; private set; }
    public Guid? WorkflowId { get; private set; }
    public Guid? PromptTemplateId { get; private set; }
    public Guid? KnowledgePackageId { get; private set; }
    public Guid? TenantId { get; private set; }
    public int EstimatedPromptTokens { get; private set; }

    private ExecutionContext() { } // For EF Core

    private ExecutionContext(Guid? conversationId, Guid? workflowId, Guid? promptTemplateId, Guid? knowledgePackageId, Guid? tenantId, int estimatedPromptTokens)
    {
        if (estimatedPromptTokens < 0) throw new ArgumentException("Estimated prompt tokens cannot be negative.");
        ConversationId = conversationId;
        WorkflowId = workflowId;
        PromptTemplateId = promptTemplateId;
        KnowledgePackageId = knowledgePackageId;
        TenantId = tenantId;
        EstimatedPromptTokens = estimatedPromptTokens;
    }

    public static ExecutionContext Create(
        Guid? conversationId = null,
        Guid? workflowId = null,
        Guid? promptTemplateId = null,
        Guid? knowledgePackageId = null,
        Guid? tenantId = null,
        int estimatedPromptTokens = 0)
        => new(conversationId, workflowId, promptTemplateId, knowledgePackageId, tenantId, estimatedPromptTokens);

    public static ExecutionContext Empty() => new(null, null, null, null, null, 0);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ConversationId ?? Guid.Empty;
        yield return WorkflowId ?? Guid.Empty;
        yield return PromptTemplateId ?? Guid.Empty;
        yield return KnowledgePackageId ?? Guid.Empty;
        yield return TenantId ?? Guid.Empty;
        yield return EstimatedPromptTokens;
    }
}

/// <summary>
/// What the caller requires from the execution: capabilities, latency,
/// streaming, tools, and structured output. The planner matches this against
/// the model catalogue.
/// </summary>
public class ExecutionRequirement : ValueObject
{
    public CapabilitySet RequiredCapabilities { get; private set; } = CapabilitySet.Empty();
    public LatencyTarget Latency { get; private set; } = LatencyTarget.Default();
    public bool RequiresStreaming { get; private set; }
    public bool RequiresTools { get; private set; }
    public bool RequiresStructuredOutput { get; private set; }
    public int MaxOutputTokens { get; private set; }

    private ExecutionRequirement() { } // For EF Core

    private ExecutionRequirement(CapabilitySet capabilities, LatencyTarget latency, bool streaming, bool tools, bool structured, int maxOutputTokens)
    {
        if (maxOutputTokens < 0) throw new ArgumentException("MaxOutputTokens cannot be negative.");

        // Requirements imply capabilities — keep them consistent automatically.
        var implied = capabilities;
        if (streaming) implied = implied.With(IntelligenceCapability.Streaming);
        if (tools) implied = implied.With(IntelligenceCapability.ToolCalling);
        if (structured) implied = implied.With(IntelligenceCapability.StructuredOutput);

        RequiredCapabilities = implied;
        Latency = latency;
        RequiresStreaming = streaming;
        RequiresTools = tools;
        RequiresStructuredOutput = structured;
        MaxOutputTokens = maxOutputTokens;
    }

    public static ExecutionRequirement Create(
        CapabilitySet? capabilities = null,
        LatencyTarget? latency = null,
        bool requiresStreaming = false,
        bool requiresTools = false,
        bool requiresStructuredOutput = false,
        int maxOutputTokens = 2048)
        => new(capabilities ?? CapabilitySet.Of(IntelligenceCapability.Chat),
               latency ?? LatencyTarget.Default(),
               requiresStreaming, requiresTools, requiresStructuredOutput, maxOutputTokens);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequiredCapabilities;
        yield return Latency;
        yield return RequiresStreaming;
        yield return RequiresTools;
        yield return RequiresStructuredOutput;
        yield return MaxOutputTokens;
    }
}

/// <summary>
/// Execution policy: the guardrails a plan must satisfy. References tenant and
/// safety policies by identity; the Policy Engine decides, this records.
/// </summary>
public class ExecutionPolicy : ValueObject
{
    public ExecutionCost MaxCostPerExecution { get; private set; } = ExecutionCost.Zero();
    public bool AllowFallback { get; private set; }
    public bool AllowStreaming { get; private set; }
    public bool AllowTools { get; private set; }
    public TimeSpan Timeout { get; private set; }
    public Guid? TenantPolicyId { get; private set; }
    public Guid? SafetyPolicyId { get; private set; }

    private ExecutionPolicy() { } // For EF Core

    private ExecutionPolicy(ExecutionCost maxCost, bool allowFallback, bool allowStreaming, bool allowTools, TimeSpan timeout, Guid? tenantPolicyId, Guid? safetyPolicyId)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentException("Timeout must be positive.");
        MaxCostPerExecution = maxCost;
        AllowFallback = allowFallback;
        AllowStreaming = allowStreaming;
        AllowTools = allowTools;
        Timeout = timeout;
        TenantPolicyId = tenantPolicyId;
        SafetyPolicyId = safetyPolicyId;
    }

    public static ExecutionPolicy Create(
        ExecutionCost? maxCostPerExecution = null,
        bool allowFallback = true,
        bool allowStreaming = true,
        bool allowTools = true,
        TimeSpan? timeout = null,
        Guid? tenantPolicyId = null,
        Guid? safetyPolicyId = null)
        => new(maxCostPerExecution ?? ExecutionCost.Create(1.00m),
               allowFallback, allowStreaming, allowTools,
               timeout ?? TimeSpan.FromSeconds(60),
               tenantPolicyId, safetyPolicyId);

    public static ExecutionPolicy Default() => Create();

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return MaxCostPerExecution;
        yield return AllowFallback;
        yield return AllowStreaming;
        yield return AllowTools;
        yield return Timeout;
        yield return TenantPolicyId ?? Guid.Empty;
        yield return SafetyPolicyId ?? Guid.Empty;
    }
}

/// <summary>
/// The outcome of one safety pipeline stage. The Intelligence Engine
/// coordinates the pipeline; dedicated engines implement detection later and
/// are referenced by id, never invoked directly from the domain.
/// </summary>
public class SafetyCheck : ValueObject
{
    public SafetyStage Stage { get; private set; }
    public SafetyVerdict Verdict { get; private set; }
    public string Detail { get; private set; } = string.Empty;
    public Guid? EvaluatorReference { get; private set; }

    private SafetyCheck() { } // For EF Core

    private SafetyCheck(SafetyStage stage, SafetyVerdict verdict, string detail, Guid? evaluatorReference)
    {
        Stage = stage;
        Verdict = verdict;
        Detail = detail ?? string.Empty;
        EvaluatorReference = evaluatorReference;
    }

    public static SafetyCheck Passed(SafetyStage stage, Guid? evaluatorReference = null)
        => new(stage, SafetyVerdict.Approved, string.Empty, evaluatorReference);

    public static SafetyCheck Warning(SafetyStage stage, string detail, Guid? evaluatorReference = null)
        => new(stage, SafetyVerdict.ApprovedWithWarnings, detail, evaluatorReference);

    public static SafetyCheck Rejected(SafetyStage stage, string detail, Guid? evaluatorReference = null)
        => new(stage, SafetyVerdict.Rejected, detail, evaluatorReference);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Stage;
        yield return Verdict;
        yield return Detail;
        yield return EvaluatorReference ?? Guid.Empty;
    }
}

/// <summary>
/// The aggregate safety decision across all executed stages: rejected if any
/// stage rejected, warned if any stage warned, approved otherwise.
/// </summary>
public class SafetyDecision : ValueObject
{
    private readonly List<SafetyCheck> _checks = new();
    public IReadOnlyList<SafetyCheck> Checks => _checks.AsReadOnly();

    public SafetyVerdict Verdict =>
        _checks.Any(c => c.Verdict == SafetyVerdict.Rejected) ? SafetyVerdict.Rejected :
        _checks.Any(c => c.Verdict == SafetyVerdict.ApprovedWithWarnings) ? SafetyVerdict.ApprovedWithWarnings :
        SafetyVerdict.Approved;

    public bool IsApproved => Verdict != SafetyVerdict.Rejected;

    private SafetyDecision() { } // For EF Core

    private SafetyDecision(IEnumerable<SafetyCheck> checks) => _checks = checks.ToList();

    public static SafetyDecision From(IEnumerable<SafetyCheck> checks) => new(checks);
    public static SafetyDecision ApprovedWithoutChecks() => new(Enumerable.Empty<SafetyCheck>());

    protected override IEnumerable<object> GetEqualityComponents() => _checks.Cast<object>();
}

/// <summary>
/// A normalized, provider-independent output artifact of an execution.
/// Providers return wildly different shapes; the engine normalises them here.
/// </summary>
public class ExecutionArtifact : ValueObject
{
    public ArtifactKind Kind { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? ContentReference { get; private set; }

    private ExecutionArtifact() { } // For EF Core

    private ExecutionArtifact(ArtifactKind kind, string content, string? contentReference)
    {
        Kind = kind;
        Content = content ?? string.Empty;
        ContentReference = contentReference;
    }

    public static ExecutionArtifact Text(string content) => new(ArtifactKind.Text, content, null);
    public static ExecutionArtifact Json(string json) => new(ArtifactKind.Json, json, null);
    public static ExecutionArtifact Reference(ArtifactKind kind, string reference) => new(kind, string.Empty, reference);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Kind;
        yield return Content;
        yield return ContentReference ?? string.Empty;
    }
}

/// <summary>
/// Telemetry captured for a single execution: timings, attempts, provider
/// used, and usage. Consumed by the Trace and Evaluation engines.
/// </summary>
public class ExecutionTelemetry : ValueObject
{
    public TimeSpan TotalDuration { get; private set; }
    public TimeSpan ProviderLatency { get; private set; }
    public int Attempts { get; private set; }
    public int FallbacksUsed { get; private set; }
    public ExecutionUsage Usage { get; private set; } = ExecutionUsage.Zero();
    public ExecutionCost ActualCost { get; private set; } = ExecutionCost.Zero();

    private ExecutionTelemetry() { } // For EF Core

    private ExecutionTelemetry(TimeSpan total, TimeSpan providerLatency, int attempts, int fallbacks, ExecutionUsage usage, ExecutionCost cost)
    {
        if (attempts < 0 || fallbacks < 0) throw new ArgumentException("Counts cannot be negative.");
        TotalDuration = total;
        ProviderLatency = providerLatency;
        Attempts = attempts;
        FallbacksUsed = fallbacks;
        Usage = usage;
        ActualCost = cost;
    }

    public static ExecutionTelemetry Create(TimeSpan totalDuration, TimeSpan providerLatency, int attempts, int fallbacksUsed, ExecutionUsage usage, ExecutionCost actualCost)
        => new(totalDuration, providerLatency, attempts, fallbacksUsed, usage, actualCost);

    public static ExecutionTelemetry Empty()
        => new(TimeSpan.Zero, TimeSpan.Zero, 0, 0, ExecutionUsage.Zero(), ExecutionCost.Zero());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TotalDuration;
        yield return ProviderLatency;
        yield return Attempts;
        yield return FallbacksUsed;
        yield return Usage;
        yield return ActualCost;
    }
}

/// <summary>
/// Aggregated execution metrics over a period, per provider/model dimension.
/// Feeds provider health assessment and platform analytics.
/// </summary>
public class ExecutionMetrics : ValueObject
{
    public int TotalExecutions { get; private set; }
    public int SuccessfulExecutions { get; private set; }
    public int FailedExecutions { get; private set; }
    public TimeSpan AverageLatency { get; private set; }
    public ExecutionUsage TotalUsage { get; private set; } = ExecutionUsage.Zero();
    public ExecutionCost TotalCost { get; private set; } = ExecutionCost.Zero();

    public double SuccessRate => TotalExecutions == 0 ? 1.0 : (double)SuccessfulExecutions / TotalExecutions;

    private ExecutionMetrics() { } // For EF Core

    private ExecutionMetrics(int total, int success, int failed, TimeSpan avgLatency, ExecutionUsage usage, ExecutionCost cost)
    {
        if (total < 0 || success < 0 || failed < 0) throw new ArgumentException("Counts cannot be negative.");
        if (success + failed > total) throw new ArgumentException("Success + failed cannot exceed total.");
        TotalExecutions = total;
        SuccessfulExecutions = success;
        FailedExecutions = failed;
        AverageLatency = avgLatency;
        TotalUsage = usage;
        TotalCost = cost;
    }

    public static ExecutionMetrics Create(int total, int successful, int failed, TimeSpan averageLatency, ExecutionUsage totalUsage, ExecutionCost totalCost)
        => new(total, successful, failed, averageLatency, totalUsage, totalCost);

    public static ExecutionMetrics Empty()
        => new(0, 0, 0, TimeSpan.Zero, ExecutionUsage.Zero(), ExecutionCost.Zero());

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TotalExecutions;
        yield return SuccessfulExecutions;
        yield return FailedExecutions;
        yield return AverageLatency;
        yield return TotalUsage;
        yield return TotalCost;
    }
}
