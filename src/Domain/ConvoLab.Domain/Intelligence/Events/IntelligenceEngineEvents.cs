using ConvoLab.Domain.Events;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Events;

/// <summary>Raised when an intelligent execution is requested by the platform.</summary>
public record ExecutionRequestedEvent(ExecutionRequestId RequestId, ValueObjects.ExecutionContext Context) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when the Execution Planner produces an immutable plan.</summary>
public record ExecutionPlannedEvent(ExecutionRequestId RequestId, ExecutionPlanId PlanId, IntelligenceProviderId ProviderId, IntelligenceModelId ModelId) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a provider is selected during planning.</summary>
public record ProviderSelectedEvent(ExecutionRequestId RequestId, IntelligenceProviderId ProviderId, string ProviderName) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a model is selected during planning.</summary>
public record ModelSelectedEvent(ExecutionRequestId RequestId, IntelligenceModelId ModelId, string ModelName) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when the safety pipeline validates an execution.</summary>
public record ExecutionValidatedEvent(ExecutionRequestId RequestId, SafetyVerdict Verdict) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when execution begins against the planned provider.</summary>
public record ExecutionStartedEvent(ExecutionRequestId RequestId, ExecutionPlanId PlanId) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a streaming session opens for an execution.</summary>
public record StreamingStartedEvent(ExecutionRequestId RequestId, StreamingSessionId SessionId) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a streaming session completes.</summary>
public record StreamingCompletedEvent(ExecutionRequestId RequestId, StreamingSessionId SessionId, StreamingStatistics Statistics) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a tool is invoked during an execution.</summary>
public record ToolInvokedEvent(ExecutionRequestId RequestId, ToolInvocationId InvocationId, string ToolName, ToolKind Kind) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a function is invoked during an execution.</summary>
public record FunctionInvokedEvent(ExecutionRequestId RequestId, ToolInvocationId InvocationId, string FunctionName) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when an execution completes successfully.</summary>
public record ExecutionCompletedEvent(ExecutionRequestId RequestId, ExecutionUsage Usage, ExecutionCost ActualCost) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised on each retry attempt.</summary>
public record ExecutionRetriedEvent(ExecutionRequestId RequestId, int AttemptNumber, FailureKind Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when an execution fails beyond recovery.</summary>
public record ExecutionFailedEvent(ExecutionRequestId RequestId, FailureKind Kind, string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a fallback model is executed after primary failure.</summary>
public record FallbackExecutedEvent(ExecutionRequestId RequestId, IntelligenceModelId FallbackModelId, int FallbackPosition) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when an execution is cancelled by the caller.</summary>
public record ExecutionCancelledEvent(ExecutionRequestId RequestId, string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when an execution result is evaluated.</summary>
public record ExecutionEvaluatedEvent(ExecutionRequestId RequestId) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when execution telemetry is recorded for tracing/analytics.</summary>
public record ExecutionRecordedEvent(ExecutionRequestId RequestId, ExecutionTelemetry Telemetry) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a provider is registered in the catalogue.</summary>
public record ProviderRegisteredEvent(IntelligenceProviderId ProviderId, string Name, ProviderKind Kind) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when provider health changes materially (e.g. circuit opens).</summary>
public record ProviderHealthChangedEvent(IntelligenceProviderId ProviderId, ProviderAvailability Availability, CircuitStatus Circuit) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when budget consumption is published for usage reporting.</summary>
public record UsagePublishedEvent(ExecutionBudgetId BudgetId, ExecutionCost Consumed, ExecutionCost Remaining) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a budget is exhausted — planning must halt executions.</summary>
public record BudgetExhaustedEvent(ExecutionBudgetId BudgetId, ExecutionCost Limit) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
