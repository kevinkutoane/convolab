using ConvoLab.Domain.Common;
using ConvoLab.Domain.Execution.Enums;

namespace ConvoLab.Domain.Execution.ValueObjects;

public class ExecutionResult : ValueObject
{
    public ExecutionStatus Status { get; private set; }
    public string? Message { get; private set; }
    public DateTime Timestamp { get; private set; }

    private ExecutionResult(ExecutionStatus status, string? message)
    {
        Status = status;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }

    public static ExecutionResult Success(string? message = null)
    {
        return new ExecutionResult(ExecutionStatus.Completed, message);
    }

    public static ExecutionResult Failure(string message)
    {
        return new ExecutionResult(ExecutionStatus.Failed, message);
    }

    public static ExecutionResult Cancelled(string? message = null)
    {
        return new ExecutionResult(ExecutionStatus.Cancelled, message);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Status;
        yield return Message ?? string.Empty;
        yield return Timestamp; // Consider if Timestamp should be part of equality
    }

    // For EF Core
    private ExecutionResult() { }
}
