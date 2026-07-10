using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IWorkflowEngine
{
    Task<ExecutionResult> ExecuteAsync(Pipeline pipeline, ValueObjects.ExecutionContext context);
}
