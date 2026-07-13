namespace ConvoLab.Domain.Execution.Enums;

public enum ExecutionStatus
{
    Created,
    Queued,
    Running,
    PreparingPrompt,
    RetrievingKnowledge,
    CallingAI,
    EvaluatingResponse,
    RecordingTrace,
    Completed,
    Failed,
    Cancelled
}
