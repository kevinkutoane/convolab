using ConvoLab.Domain.Common;
using ConvoLab.Domain.Execution.Enums;
using ConvoLab.Domain.Execution.Entities;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Execution.Aggregates;

public class Pipeline : BaseAggregateRoot<ExecutionId>
{
    public string Name { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public ExecutionResult? OverallResult { get; private set; }

    private readonly List<PipelineStep> _steps = new();
    public IReadOnlyCollection<PipelineStep> Steps => _steps.AsReadOnly();

    private Pipeline(string name, ExecutionId id)
        : base(id)
    {
        Name = name;
        Status = ExecutionStatus.Pending;
        StartTime = DateTime.UtcNow;
    }

    public static Pipeline Create(string name)
    {
        return new Pipeline(name, ExecutionId.CreateUnique());
    }

    public PipelineStep AddStep(string stepName)
    {
        var step = PipelineStep.Create(stepName, Id.Value);
        _steps.Add(step);
        return step;
    }

    public void Start()
    {
        if (Status != ExecutionStatus.Pending)
        {
            throw new InvalidOperationException("Pipeline can only be started from Pending status.");
        }
        Status = ExecutionStatus.Running;
        StartTime = DateTime.UtcNow;
    }

    public void Complete(ExecutionResult result)
    {
        if (Status != ExecutionStatus.Running)
        {
            throw new InvalidOperationException("Pipeline can only be completed from Running status.");
        }
        Status = result.Status;
        OverallResult = result;
        EndTime = DateTime.UtcNow;
        Duration = EndTime - StartTime;
    }

    public void Fail(string errorMessage)
    {
        if (Status != ExecutionStatus.Running)
        {
            throw new InvalidOperationException("Pipeline can only be failed from Running status.");
        }
        Status = ExecutionStatus.Failed;
        OverallResult = ExecutionResult.Failure(errorMessage);
        EndTime = DateTime.UtcNow;
        Duration = EndTime - StartTime;
    }

    public void Cancel(string? message = null)
    {
        if (Status == ExecutionStatus.Completed || Status == ExecutionStatus.Failed)
        {
            throw new InvalidOperationException("Cannot cancel a completed or failed pipeline.");
        }
        Status = ExecutionStatus.Cancelled;
        OverallResult = ExecutionResult.Cancelled(message);
        EndTime = DateTime.UtcNow;
        Duration = EndTime - StartTime;
    }

    // For EF Core
    private Pipeline() { 
        Name = null!;
        Status = ExecutionStatus.Pending;
        StartTime = DateTime.UtcNow;
    }
}
