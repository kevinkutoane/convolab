using ConvoLab.Application.Operations;

namespace ConvoLab.Application.Tests.Operations;

public sealed class OperationalEvidenceTests
{
    private static readonly OperationsThresholdOptions Thresholds = new();

    [Fact]
    public void Empty_pipeline_is_healthy()
    {
        Assert.Equal(
            OperationalStatusLevel.Healthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(Evidence(), Thresholds));
    }

    [Fact]
    public void Fresh_pending_work_is_healthy()
    {
        var evidence = Evidence(pending: 1, pendingAge: 5);

        Assert.Equal(
            OperationalStatusLevel.Healthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(evidence, Thresholds));
    }

    [Fact]
    public void Old_pending_and_failed_work_change_status_truthfully()
    {
        Assert.Equal(
            OperationalStatusLevel.Degraded,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(pending: 1, pendingAge: 61), Thresholds));
        Assert.Equal(
            OperationalStatusLevel.Unhealthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(pending: 1, pendingAge: 301), Thresholds));
        Assert.Equal(
            OperationalStatusLevel.Degraded,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(failed: 1, failedAge: 10), Thresholds));
        Assert.Equal(
            OperationalStatusLevel.Unhealthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(failed: 10, failedAge: 10), Thresholds));
        Assert.Equal(
            OperationalStatusLevel.Unhealthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(failed: 1, failedAge: 300), Thresholds));
    }

    [Fact]
    public void Aggregation_failure_and_partial_worker_failure_are_degraded()
    {
        Assert.Equal(
            OperationalStatusLevel.Degraded,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(failedCheckpoints: 1), Thresholds));
        Assert.Equal(
            OperationalStatusLevel.Degraded,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(), Thresholds, recentPartialWorkerFailure: true));
    }

    [Fact]
    public void Aggregation_lag_uses_the_configured_warning_and_unhealthy_thresholds()
    {
        var thresholds = new OperationsThresholdOptions
        {
            AggregationWarningSeconds = 25,
            AggregationUnhealthySeconds = 50
        };

        Assert.Equal(
            OperationalStatusLevel.Healthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(aggregationLag: 24), thresholds));
        Assert.Equal(
            OperationalStatusLevel.Degraded,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(aggregationLag: 25), thresholds));
        Assert.Equal(
            OperationalStatusLevel.Unhealthy,
            AnalyticsPipelineStatusEvaluator.Evaluate(
                Evidence(aggregationLag: 50), thresholds));
    }

    [Fact]
    public void Operational_options_reject_inverted_or_unsafe_values()
    {
        Assert.True(OperationsThresholdOptions.IsValid(new()));
        Assert.False(OperationsThresholdOptions.IsValid(new()
        {
            OutboxWarningSeconds = 300,
            OutboxUnhealthySeconds = 60
        }));
        Assert.True(AnalyticsWorkerOptions.IsValid(new()));
        Assert.False(AnalyticsWorkerOptions.IsValid(new()
        {
            LeaseDurationSeconds = 30,
            LeaseRenewalSeconds = 30
        }));
    }

    private static AnalyticsPipelineEvidence Evidence(
        int pending = 0,
        double? pendingAge = null,
        int failed = 0,
        double? failedAge = null,
        int failedCheckpoints = 0,
        double aggregationLag = 0) => new(
            pending,
            failed,
            pendingAge,
            failedAge,
            0,
            failedCheckpoints,
            aggregationLag,
            null,
            null);
}
