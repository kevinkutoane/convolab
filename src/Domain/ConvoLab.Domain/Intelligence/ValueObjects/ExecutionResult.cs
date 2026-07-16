using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>
/// The normalized, provider-independent result of an execution: artifacts,
/// usage, actual cost, and the finish disposition. Providers return different
/// shapes; the engine normalises them all into this single contract before
/// anything downstream sees them.
/// </summary>
public class ExecutionResult : ValueObject
{
    private readonly List<ExecutionArtifact> _artifacts = new();

    public IReadOnlyList<ExecutionArtifact> Artifacts => _artifacts.AsReadOnly();
    public ExecutionUsage Usage { get; private set; } = ExecutionUsage.Zero();
    public ExecutionCost ActualCost { get; private set; } = ExecutionCost.Zero();
    public string FinishReason { get; private set; } = string.Empty;

    /// <summary>The primary text artifact, when one exists.</summary>
    public string PrimaryText => _artifacts.FirstOrDefault(a => a.Kind == ArtifactKind.Text)?.Content ?? string.Empty;

    private ExecutionResult() { } // For EF Core

    private ExecutionResult(IEnumerable<ExecutionArtifact> artifacts, ExecutionUsage usage, ExecutionCost actualCost, string finishReason)
    {
        _artifacts = artifacts.ToList();
        Usage = usage;
        ActualCost = actualCost;
        FinishReason = finishReason ?? string.Empty;
    }

    public static ExecutionResult Create(IEnumerable<ExecutionArtifact> artifacts, ExecutionUsage usage, ExecutionCost actualCost, string finishReason = "stop")
        => new(artifacts, usage, actualCost, finishReason);

    public static ExecutionResult FromText(string text, ExecutionUsage usage, ExecutionCost actualCost)
        => new(new[] { ExecutionArtifact.Text(text) }, usage, actualCost, "stop");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var artifact in _artifacts) yield return artifact;
        yield return Usage;
        yield return ActualCost;
        yield return FinishReason;
    }
}

/// <summary>
/// The response returned to callers of the Intelligence Engine: the request
/// identity, final status, result (when successful), and telemetry. Callers
/// never see providers, SDKs, or wire formats.
/// </summary>
public class ExecutionResponse : ValueObject
{
    public ExecutionRequestId RequestId { get; private set; } = null!;
    public ExecutionStatus Status { get; private set; }
    public ExecutionResult? Result { get; private set; }
    public ExecutionTelemetry Telemetry { get; private set; } = ExecutionTelemetry.Empty();
    public string? FailureReason { get; private set; }

    public bool IsSuccess => Result is not null && Status is ExecutionStatus.Finished
        or ExecutionStatus.Recorded or ExecutionStatus.Evaluated or ExecutionStatus.Completed;

    private ExecutionResponse() { } // For EF Core

    private ExecutionResponse(ExecutionRequestId requestId, ExecutionStatus status, ExecutionResult? result, ExecutionTelemetry telemetry, string? failureReason)
    {
        RequestId = requestId;
        Status = status;
        Result = result;
        Telemetry = telemetry;
        FailureReason = failureReason;
    }

    public static ExecutionResponse Success(ExecutionRequestId requestId, ExecutionStatus status, ExecutionResult result, ExecutionTelemetry telemetry)
        => new(requestId, status, result, telemetry, null);

    public static ExecutionResponse Failure(ExecutionRequestId requestId, ExecutionStatus status, string reason, ExecutionTelemetry? telemetry = null)
        => new(requestId, status, null, telemetry ?? ExecutionTelemetry.Empty(), reason);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequestId;
        yield return Status;
        yield return Result ?? (object)string.Empty;
        yield return Telemetry;
        yield return FailureReason ?? string.Empty;
    }
}
