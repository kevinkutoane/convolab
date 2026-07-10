using ConvoLab.Domain.Common;
using ConvoLab.Domain.Execution.Enums;
using ConvoLab.Domain.Execution.Entities;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Execution.Aggregates;

public class WorkflowExecution : BaseAggregateRoot<ExecutionId>
{
    public string Name { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public ExecutionResult? OverallResult { get; private set; }
    public Guid WorkflowVersionId { get; private set; }

    private readonly List<WorkflowStep> _steps = new();
    public IReadOnlyCollection<WorkflowStep> Steps => _steps.AsReadOnly();

    private WorkflowExecution(string name, ExecutionId id, Guid workflowVersionId)
        : base(id)
    {
        Name = name;
        WorkflowVersionId = workflowVersionId;
        Status = ExecutionStatus.Created;
        StartTime = DateTime.UtcNow;
    }

    public static WorkflowExecution Create(string name, Guid workflowVersionId)
    {
        return new WorkflowExecution(name, ExecutionId.CreateUnique(), workflowVersionId);
    }

    public WorkflowStep AddStep(string stepName)
    {
        var step = WorkflowStep.Create(stepName, Id.Value);
        _steps.Add(step);
        return step;
    }

    public void TransitionTo(ExecutionStatus nextStatus)
    {
        if (!CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Invalid state transition from {Status} to {nextStatus}.");
        }

        Status = nextStatus;

        if (nextStatus == ExecutionStatus.Running && StartTime == default)
        {
            StartTime = DateTime.UtcNow;
        }

        if (IsTerminalStatus(nextStatus))
        {
            EndTime = DateTime.UtcNow;
            Duration = EndTime - StartTime;
        }
    }

    private bool CanTransitionTo(ExecutionStatus nextStatus)
    {
        return (Status, nextStatus) switch
        {
            (ExecutionStatus.Created, ExecutionStatus.Queued) => true,
            (ExecutionStatus.Created, ExecutionStatus.Running) => true,
            (ExecutionStatus.Created, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.Queued, ExecutionStatus.Running) => true,
            (ExecutionStatus.Queued, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.Running, ExecutionStatus.PreparingPrompt) => true,
            (ExecutionStatus.Running, ExecutionStatus.RetrievingKnowledge) => true,
            (ExecutionStatus.Running, ExecutionStatus.CallingAI) => true,
            (ExecutionStatus.Running, ExecutionStatus.EvaluatingResponse) => true,
            (ExecutionStatus.Running, ExecutionStatus.RecordingTrace) => true,
            (ExecutionStatus.Running, ExecutionStatus.Completed) => true,
            (ExecutionStatus.Running, ExecutionStatus.Failed) => true,
            (ExecutionStatus.Running, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.PreparingPrompt, ExecutionStatus.RetrievingKnowledge) => true,
            (ExecutionStatus.PreparingPrompt, ExecutionStatus.CallingAI) => true,
            (ExecutionStatus.PreparingPrompt, ExecutionStatus.Failed) => true,
            (ExecutionStatus.PreparingPrompt, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.RetrievingKnowledge, ExecutionStatus.PreparingPrompt) => true,
            (ExecutionStatus.RetrievingKnowledge, ExecutionStatus.CallingAI) => true,
            (ExecutionStatus.RetrievingKnowledge, ExecutionStatus.Failed) => true,
            (ExecutionStatus.RetrievingKnowledge, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.CallingAI, ExecutionStatus.EvaluatingResponse) => true,
            (ExecutionStatus.CallingAI, ExecutionStatus.RecordingTrace) => true,
            (ExecutionStatus.CallingAI, ExecutionStatus.Completed) => true,
            (ExecutionStatus.CallingAI, ExecutionStatus.Failed) => true,
            (ExecutionStatus.CallingAI, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.EvaluatingResponse, ExecutionStatus.RecordingTrace) => true,
            (ExecutionStatus.EvaluatingResponse, ExecutionStatus.Completed) => true,
            (ExecutionStatus.EvaluatingResponse, ExecutionStatus.Failed) => true,
            (ExecutionStatus.EvaluatingResponse, ExecutionStatus.Cancelled) => true,
            
            (ExecutionStatus.RecordingTrace, ExecutionStatus.Completed) => true,
            (ExecutionStatus.RecordingTrace, ExecutionStatus.Failed) => true,
            (ExecutionStatus.RecordingTrace, ExecutionStatus.Cancelled) => true,
            
            _ => false
        };
    }

    private bool IsTerminalStatus(ExecutionStatus status)
    {
        return status == ExecutionStatus.Completed || 
               status == ExecutionStatus.Failed || 
               status == ExecutionStatus.Cancelled;
    }

    public void Complete(ExecutionResult result)
    {
        TransitionTo(ExecutionStatus.Completed);
        OverallResult = result;
    }

    public void Fail(string errorMessage)
    {
        TransitionTo(ExecutionStatus.Failed);
        OverallResult = ExecutionResult.Failure(errorMessage);
    }

    public void Cancel(string? message = null)
    {
        TransitionTo(ExecutionStatus.Cancelled);
        OverallResult = ExecutionResult.Cancelled(message);
    }

    // For EF Core
    private WorkflowExecution() { 
        Name = null!;
        Status = ExecutionStatus.Created;
        StartTime = DateTime.UtcNow;
    }
}
