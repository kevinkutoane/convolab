using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Entities;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Aggregates;

/// <summary>
/// The central aggregate of the Intelligence Engine: one intelligent
/// execution, from request to finish. Enforces the lifecycle
/// Requested → Planned → Validated → Executing → (Streaming) → Completed →
/// Evaluated → Recorded → Finished, with cancellation, timeout, retry, and
/// fallback as governed transitions — never ad-hoc mutations.
/// </summary>
public class ExecutionRequest : BaseAggregateRoot<ExecutionRequestId>
{
    private readonly List<ToolInvocation> _toolInvocations = new();
    private readonly List<ExecutionFailure> _failures = new();

    public ValueObjects.ExecutionContext Context { get; private set; }
    public ExecutionRequirement Requirement { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public ExecutionPlan? Plan { get; private set; }
    public SafetyDecision? SafetyDecision { get; private set; }
    public StreamingSession? StreamingSession { get; private set; }
    public ExecutionResult? Result { get; private set; }
    public ExecutionTelemetry? Telemetry { get; private set; }

    public int AttemptNumber { get; private set; }
    public int FallbacksUsed { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    public IReadOnlyList<ToolInvocation> ToolInvocations => _toolInvocations.AsReadOnly();
    public IReadOnlyList<ExecutionFailure> Failures => _failures.AsReadOnly();

    private ExecutionRequest() : base()
    {
        Context = null!;
        Requirement = null!;
    } // For EF Core

    private ExecutionRequest(ExecutionRequestId id, ValueObjects.ExecutionContext context, ExecutionRequirement requirement) : base(id)
    {
        Context = context;
        Requirement = requirement;
        Status = ExecutionStatus.Requested;
        AttemptNumber = 0;
        RequestedAt = DateTime.UtcNow;
    }

    public static ExecutionRequest Create(ValueObjects.ExecutionContext context, ExecutionRequirement requirement)
    {
        var request = new ExecutionRequest(ExecutionRequestId.CreateUnique(), context, requirement);
        request.AddDomainEvent(new ExecutionRequestedEvent(request.Id, context));
        return request;
    }

    // ── Lifecycle: planning ─────────────────────────────────────────────

    /// <summary>Attaches the immutable plan produced by the Execution Planner.</summary>
    public void AttachPlan(ExecutionPlan plan)
    {
        EnsureStatus(ExecutionStatus.Requested, "attach a plan");
        Plan = plan;
        Status = ExecutionStatus.Planned;

        AddDomainEvent(new ProviderSelectedEvent(Id, plan.ProviderId, plan.ProviderName));
        AddDomainEvent(new ModelSelectedEvent(Id, plan.ModelId, plan.ModelName));
        AddDomainEvent(new ExecutionPlannedEvent(Id, plan.Id, plan.ProviderId, plan.ModelId));
    }

    // ── Lifecycle: safety validation ────────────────────────────────────

    /// <summary>
    /// Records the safety pipeline decision. A rejection terminates the
    /// execution immediately — rejected work never reaches a provider.
    /// </summary>
    public void Validate(SafetyDecision decision)
    {
        EnsureStatus(ExecutionStatus.Planned, "validate");
        SafetyDecision = decision;

        AddDomainEvent(new ExecutionValidatedEvent(Id, decision.Verdict));

        if (!decision.IsApproved)
        {
            var reason = string.Join("; ", decision.Checks
                .Where(c => c.Verdict == SafetyVerdict.Rejected)
                .Select(c => $"{c.Stage}: {c.Detail}"));
            RecordFailureInternal(FailureKind.SafetyRejection, reason);
            Status = ExecutionStatus.Failed;
            FinishedAt = DateTime.UtcNow;
            AddDomainEvent(new ExecutionFailedEvent(Id, FailureKind.SafetyRejection, reason));
            return;
        }

        Status = ExecutionStatus.Validated;
    }

    // ── Lifecycle: execution ────────────────────────────────────────────

    public void Begin()
    {
        EnsureStatus(ExecutionStatus.Validated, "begin execution");
        AttemptNumber++;
        StartedAt ??= DateTime.UtcNow;
        Status = ExecutionStatus.Executing;
        AddDomainEvent(new ExecutionStartedEvent(Id, Plan!.Id));
    }

    /// <summary>Opens a streaming session; only valid when the plan enables streaming.</summary>
    public StreamingSession OpenStream()
    {
        EnsureStatus(ExecutionStatus.Executing, "open a stream");
        if (Plan is null || !Plan.UseStreaming)
            throw new InvalidOperationException("The execution plan does not enable streaming.");

        StreamingSession = new StreamingSession(StreamingSessionId.CreateUnique());
        Status = ExecutionStatus.Streaming;
        AddDomainEvent(new StreamingStartedEvent(Id, StreamingSession.Id));
        return StreamingSession;
    }

    /// <summary>Closes the streaming session and returns to Executing for completion.</summary>
    public StreamingCompletion CompleteStream()
    {
        EnsureStatus(ExecutionStatus.Streaming, "complete the stream");
        var completion = StreamingSession!.Complete();
        Status = ExecutionStatus.Executing;
        AddDomainEvent(new StreamingCompletedEvent(Id, StreamingSession.Id, completion.Statistics));
        return completion;
    }

    // ── Tool calling ────────────────────────────────────────────────────

    /// <summary>Registers a tool invocation requested by the model mid-execution.</summary>
    public ToolInvocation InvokeTool(string toolName, ToolKind kind, string argumentsPayload)
    {
        if (Status is not (ExecutionStatus.Executing or ExecutionStatus.Streaming))
            throw new InvalidOperationException($"Cannot invoke tools in status '{Status}'.");
        if (Plan is null || !Plan.AllowTools)
            throw new InvalidOperationException("The execution plan does not allow tool calling.");

        var invocation = new ToolInvocation(ToolInvocationId.CreateUnique(), toolName, kind, argumentsPayload);
        _toolInvocations.Add(invocation);

        AddDomainEvent(new ToolInvokedEvent(Id, invocation.Id, toolName, kind));
        if (invocation.IsFunctionInvocation)
            AddDomainEvent(new FunctionInvokedEvent(Id, invocation.Id, toolName));

        return invocation;
    }

    // ── Lifecycle: completion, evaluation, recording ────────────────────

    public void Complete(ExecutionResult result)
    {
        if (Status is not (ExecutionStatus.Executing or ExecutionStatus.Streaming))
            throw new InvalidOperationException($"Cannot complete an execution in status '{Status}'.");

        Result = result;
        Status = ExecutionStatus.Completed;
        AddDomainEvent(new ExecutionCompletedEvent(Id, result.Usage, result.ActualCost));
    }

    public void MarkEvaluated()
    {
        EnsureStatus(ExecutionStatus.Completed, "evaluate");
        Status = ExecutionStatus.Evaluated;
        AddDomainEvent(new ExecutionEvaluatedEvent(Id));
    }

    public void Record(ExecutionTelemetry telemetry)
    {
        EnsureStatus(ExecutionStatus.Evaluated, "record");
        Telemetry = telemetry;
        Status = ExecutionStatus.Recorded;
        AddDomainEvent(new ExecutionRecordedEvent(Id, telemetry));
    }

    public void Finish()
    {
        EnsureStatus(ExecutionStatus.Recorded, "finish");
        Status = ExecutionStatus.Finished;
        FinishedAt = DateTime.UtcNow;
    }

    // ── Failure handling: retry, fallback, cancel, timeout ─────────────

    /// <summary>
    /// Records a failure and decides the next transition: retry (back to
    /// Validated), await fallback, or terminal failure.
    /// </summary>
    public FailureDisposition RegisterFailure(FailureKind kind, string reason)
    {
        if (Status is not (ExecutionStatus.Executing or ExecutionStatus.Streaming))
            throw new InvalidOperationException($"Cannot register a failure in status '{Status}'.");

        // A failure mid-stream aborts the stream.
        if (Status == ExecutionStatus.Streaming)
            StreamingSession?.Abort();

        RecordFailureInternal(kind, reason);

        if (Plan!.RetryPolicy.CanRetry(AttemptNumber, kind))
        {
            Status = ExecutionStatus.Validated; // Eligible to Begin() again
            AddDomainEvent(new ExecutionRetriedEvent(Id, AttemptNumber, kind));
            return FailureDisposition.Retry;
        }

        var nextFallback = Plan.FallbackPolicy.NextAfter(FallbacksUsed);
        if (Plan.Policy.AllowFallback && nextFallback is not null)
        {
            Status = ExecutionStatus.Validated;
            return FailureDisposition.Fallback;
        }

        Status = kind == FailureKind.Timeout ? ExecutionStatus.TimedOut : ExecutionStatus.Failed;
        FinishedAt = DateTime.UtcNow;
        AddDomainEvent(new ExecutionFailedEvent(Id, kind, reason));
        return FailureDisposition.Terminal;
    }

    /// <summary>Switches the execution to a fallback plan after primary failure.</summary>
    public void ApplyFallbackPlan(ExecutionPlan fallbackPlan)
    {
        EnsureStatus(ExecutionStatus.Validated, "apply a fallback plan");
        if (Plan is null || !Plan.FallbackPolicy.HasFallback)
            throw new InvalidOperationException("No fallback policy is defined for this execution.");

        FallbacksUsed++;
        AttemptNumber = 0; // Fresh retry budget for the fallback model
        Plan = fallbackPlan;
        AddDomainEvent(new FallbackExecutedEvent(Id, fallbackPlan.ModelId, FallbacksUsed));
    }

    /// <summary>Cancels the execution at any non-terminal point.</summary>
    public void Cancel(string reason)
    {
        if (IsTerminal)
            throw new InvalidOperationException($"Cannot cancel an execution in terminal status '{Status}'.");

        if (Status == ExecutionStatus.Streaming)
            StreamingSession?.Abort();

        Status = ExecutionStatus.Cancelled;
        FinishedAt = DateTime.UtcNow;
        AddDomainEvent(new ExecutionCancelledEvent(Id, reason));
    }

    /// <summary>Registers a timeout against the policy's deadline.</summary>
    public FailureDisposition RegisterTimeout()
        => RegisterFailure(FailureKind.Timeout, $"Execution exceeded the {Plan?.Policy.Timeout} timeout.");

    public bool IsTerminal => Status is ExecutionStatus.Finished or ExecutionStatus.Failed
        or ExecutionStatus.Cancelled or ExecutionStatus.TimedOut;

    // ── Helpers ─────────────────────────────────────────────────────────

    private void EnsureStatus(ExecutionStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Cannot {action}: execution is '{Status}', expected '{expected}'.");
    }

    private void RecordFailureInternal(FailureKind kind, string reason)
        => _failures.Add(ExecutionFailure.Create(kind, reason));
}

/// <summary>What the engine should do after a registered failure.</summary>
public enum FailureDisposition
{
    Retry,
    Fallback,
    Terminal
}
