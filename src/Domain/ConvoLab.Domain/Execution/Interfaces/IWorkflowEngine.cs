using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IWorkflowEngine
{
    Task<ExecutionResult> ExecuteAsync(WorkflowExecution workflow, ValueObjects.ExecutionContext context);
}
