using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Intelligence;

public class ExecutionRequestTests
{
    private readonly ExecutionRequest _request;
    private readonly ExecutionPlan _plan;

    public ExecutionRequestTests()
    {
        _request = ExecutionRequest.Create(
            ConvoLab.Domain.Intelligence.ValueObjects.ExecutionContext.Empty(),
            ExecutionRequirement.Create(requiresStreaming: true, requiresTools: true));

        _plan = ExecutionPlan.Create(
            IntelligenceProviderId.CreateUnique(),
            IntelligenceModelId.CreateUnique(),
            "Provider", "Model",
            ExecutionRetryPolicy.Create(2, TimeSpan.FromMilliseconds(100)),
            ExecutionFallbackPolicy.None(),
            useStreaming: true,
            allowTools: true,
            ExecutionUsage.Zero(),
            ExecutionCost.Zero(),
            TimeSpan.FromSeconds(2),
            ExecutionPolicy.Default());
    }

    [Fact]
    public void Lifecycle_HappyPath_ShouldTransitionCorrectly()
    {
        Assert.Equal(ExecutionStatus.Requested, _request.Status);

        _request.AttachPlan(_plan);
        Assert.Equal(ExecutionStatus.Planned, _request.Status);

        _request.Validate(SafetyDecision.ApprovedWithoutChecks());
        Assert.Equal(ExecutionStatus.Validated, _request.Status);

        _request.Begin();
        Assert.Equal(ExecutionStatus.Executing, _request.Status);

        _request.OpenStream();
        Assert.Equal(ExecutionStatus.Streaming, _request.Status);

        _request.CompleteStream();
        Assert.Equal(ExecutionStatus.Executing, _request.Status);

        _request.Complete(ExecutionResult.FromText("response", ExecutionUsage.Zero(), ExecutionCost.Zero()));
        Assert.Equal(ExecutionStatus.Completed, _request.Status);

        _request.MarkEvaluated();
        Assert.Equal(ExecutionStatus.Evaluated, _request.Status);

        _request.Record(ExecutionTelemetry.Empty());
        Assert.Equal(ExecutionStatus.Recorded, _request.Status);

        _request.Finish();
        Assert.Equal(ExecutionStatus.Finished, _request.Status);
    }

    [Fact]
    public void SafetyRejection_ShouldTerminateExecution()
    {
        _request.AttachPlan(_plan);
        var decision = SafetyDecision.From(new[] { SafetyCheck.Rejected(SafetyStage.PromptPolicy, "Policy violation") });

        _request.Validate(decision);

        Assert.Equal(ExecutionStatus.Failed, _request.Status);
        Assert.True(_request.IsTerminal);
        Assert.Contains(_request.Failures, f => f.Kind == FailureKind.SafetyRejection);
    }

    [Fact]
    public void RegisterFailure_ShouldRetryWhenPolicyAllows()
    {
        _request.AttachPlan(_plan);
        _request.Validate(SafetyDecision.ApprovedWithoutChecks());
        _request.Begin();

        var disposition = _request.RegisterFailure(FailureKind.Transient, "Network error");

        Assert.Equal(FailureDisposition.Retry, disposition);
        Assert.Equal(ExecutionStatus.Validated, _request.Status); // Ready to Begin() again
    }

    [Fact]
    public void RegisterFailure_ShouldTerminateWhenRetriesExhausted()
    {
        _request.AttachPlan(_plan);
        _request.Validate(SafetyDecision.ApprovedWithoutChecks());

        _request.Begin();
        _request.RegisterFailure(FailureKind.Transient, "Error 1"); // Attempt 1 -> Retry

        _request.Begin();
        var disposition = _request.RegisterFailure(FailureKind.Transient, "Error 2"); // Attempt 2 -> Terminal (MaxAttempts = 2)

        Assert.Equal(FailureDisposition.Terminal, disposition);
        Assert.Equal(ExecutionStatus.Failed, _request.Status);
        Assert.True(_request.IsTerminal);
    }

    [Fact]
    public void InvokeTool_ShouldRegisterInvocationWhenAllowed()
    {
        _request.AttachPlan(_plan);
        _request.Validate(SafetyDecision.ApprovedWithoutChecks());
        _request.Begin();

        var tool = _request.InvokeTool("search", ToolKind.Internal, "{}");

        Assert.NotNull(tool);
        Assert.Single(_request.ToolInvocations);
        Assert.Equal(ToolInvocationStatus.Requested, tool.Status);
    }
}
