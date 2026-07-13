namespace ConvoLab.Domain.Execution.Interfaces;

public interface IEvaluationEngine
{
    Task EvaluateResponseAsync(string response, ValueObjects.ExecutionContext context);
}
