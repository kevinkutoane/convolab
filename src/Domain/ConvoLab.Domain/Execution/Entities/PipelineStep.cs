using ConvoLab.Domain.Common;
using ConvoLab.Domain.Execution.Enums;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Execution.Entities;

public class PipelineStep : BaseEntity<Guid>
{
    public string Name { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public Guid CorrelationId { get; private set; }
    public ExecutionResult? Result { get; private set; }

    private PipelineStep(string name, Guid correlationId)
        : base(Guid.NewGuid())
    {
        Name = name;
        CorrelationId = correlationId;
        Status = ExecutionStatus.Pending;
        StartTime = DateTime.UtcNow;
    }

    public static PipelineStep Create(string name, Guid correlationId)
    {
        return new PipelineStep(name, correlationId);
    }

    public void Complete(ExecutionResult result)
    {
        Status = result.Status;
        Result = result;
        EndTime = DateTime.UtcNow;
        Duration = EndTime - StartTime;
    }

    public void Fail(string errorMessage)
    {
        Status = ExecutionStatus.Failed;
        Result = ExecutionResult.Failure(errorMessage);
        EndTime = DateTime.UtcNow;
        Duration = EndTime - StartTime;
    }

    public void Cancel(string? message = null)
    {
        Status = ExecutionStatus.Cancelled;
        Result = ExecutionResult.Cancelled(message);
        EndTime = DateTime.UtcNow;
        Duration = EndTime - StartTime;
    }

    // For EF Core
    private PipelineStep() { 
        Name = null!;
        CorrelationId = Guid.Empty;
        StartTime = DateTime.UtcNow;
    }
}
