using ConvoLab.Domain.Events;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;

namespace ConvoLab.Domain.Execution.Events;

public record WorkflowStarted(ExecutionId ExecutionId, Guid WorkflowId, Guid CorrelationId, DateTime OccurredOn) : IDomainEvent;
public record WorkflowCompleted(ExecutionId ExecutionId, ExecutionResult Result, DateTime OccurredOn) : IDomainEvent;
public record WorkflowFailed(ExecutionId ExecutionId, string Error, DateTime OccurredOn) : IDomainEvent;

public record PromptPrepared(ExecutionId ExecutionId, string PromptReference, DateTime OccurredOn) : IDomainEvent;
public record KnowledgeRetrieved(ExecutionId ExecutionId, string KnowledgeReference, DateTime OccurredOn) : IDomainEvent;

public record AIInvocationStarted(ExecutionId ExecutionId, string Provider, string Model, DateTime OccurredOn) : IDomainEvent;
public record AIInvocationCompleted(ExecutionId ExecutionId, string Provider, string Model, TokenUsage Usage, DateTime OccurredOn) : IDomainEvent;

public record EvaluationCompleted(ExecutionId ExecutionId, Guid EvaluationId, string Result, DateTime OccurredOn) : IDomainEvent;
public record TraceRecorded(ExecutionId ExecutionId, Guid TraceId, DateTime OccurredOn) : IDomainEvent;

public record ConversationUpdated(ExecutionId ExecutionId, Guid ConversationId, DateTime OccurredOn) : IDomainEvent;
