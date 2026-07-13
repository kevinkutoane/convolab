namespace ConvoLab.Domain.Execution.Interfaces;

public interface ITraceEngine
{
    Task RecordSpanAsync(string name, ValueObjects.ExecutionContext context, Func<Task> action);
    Task RecordMetricAsync(string name, double value, ValueObjects.ExecutionContext context);
}
