using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.EvaluationStudio;

public sealed class EvaluationStudioService : IEvaluationStudioService
{
    private readonly IEvaluationStudioRepository _repository;
    private readonly IConversationSimulationStore _simulations;
    private static readonly SemaphoreSlim SeedGate = new(1, 1);

    public EvaluationStudioService(
        IEvaluationStudioRepository repository,
        IConversationSimulationStore simulations)
    {
        _repository = repository;
        _simulations = simulations;
    }

    public async Task<EvaluationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeSimulationRunsAsync(cancellationToken);
        var scorecards = await ListScorecardsAsync(cancellationToken);
        var runs = await ListRunsInternalAsync(500, cancellationToken);
        var testCases = (await _repository.ListTestCasesAsync(cancellationToken)).Select(Map).ToList();
        var batches = (await _repository.ListBatchesAsync(25, cancellationToken)).Select(Map).ToList();
        var passed = runs.Count(item => item.Verdict.Equals("Passed", StringComparison.OrdinalIgnoreCase));
        var review = runs.Count(item => item.Verdict.Equals("Review", StringComparison.OrdinalIgnoreCase));
        var failed = runs.Count - passed - review;
        var regressions = batches.SelectMany(item => item.Items).Count(item => !item.Passed);

        var trend = Enumerable.Range(0, 7)
            .Select(offset => DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(offset - 6)))
            .Select(date =>
            {
                var daily = runs.Where(item => DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) == date).ToList();
                var dailyPassed = daily.Count(item => item.Verdict.Equals("Passed", StringComparison.OrdinalIgnoreCase));
                return new EvaluationDailyQualityDto(
                    date,
                    daily.Count,
                    daily.Count == 0 ? 0 : daily.Average(item => item.OverallScore),
                    daily.Count == 0 ? 0 : dailyPassed / (double)daily.Count);
            })
            .ToList();

        return new EvaluationOverviewDto(
            new EvaluationMetricsDto(
                runs.Count,
                passed,
                review,
                failed,
                runs.Count == 0 ? 0 : passed / (double)runs.Count,
                runs.Count == 0 ? 0 : runs.Average(item => item.OverallScore),
                scorecards.Count(item => item.Status.Equals("Published", StringComparison.OrdinalIgnoreCase)),
                testCases.Count,
                regressions),
            trend,
            scorecards,
            runs.Take(50).ToList(),
            testCases,
            batches,
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<EvaluationScorecardDto>> ListScorecardsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultScorecardAsync(cancellationToken);
        return (await _repository.ListScorecardsAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<EvaluationScorecardDto> GetScorecardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultScorecardAsync(cancellationToken);
        var scorecard = await _repository.GetScorecardAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.scorecard.not_found", $"Evaluation scorecard '{id}' was not found.");
        return Map(scorecard);
    }

    public async Task<EvaluationScorecardDto> CreateScorecardAsync(
        CreateEvaluationScorecardCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateScorecard(command);
        var requestedVersion = string.IsNullOrWhiteSpace(command.Version) ? "1.0" : command.Version.Trim();
        var existing = await _repository.ListScorecardsAsync(cancellationToken);
        if (existing.Any(item => item.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                                 && item.Version.Equals(requestedVersion, StringComparison.OrdinalIgnoreCase)))
            throw new ResourceConflictException(
                "evaluation.scorecard.version_conflict",
                $"Evaluation scorecard '{command.Name.Trim()}' version '{requestedVersion}' already exists.");
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var metricCommands = command.Metrics is { Count: > 0 } ? command.Metrics : DefaultMetricCommands();
        var scorecard = new EvaluationScorecardState(
            id,
            command.Name.Trim(),
            command.Description.Trim(),
            "Draft",
            requestedVersion,
            ClampScore(command.QualityGateThreshold),
            command.IsDefault,
            1,
            metricCommands.Select(metric => new EvaluationMetricDefinitionState(
                Guid.NewGuid(), id, NormalizeKey(metric.Key), metric.DisplayName.Trim(), metric.Description.Trim(),
                Math.Max(0, metric.Weight), ClampScore(metric.Threshold), metric.Required)).ToList(),
            now,
            now);
        await _repository.AddScorecardAsync(scorecard, cancellationToken);
        return Map(scorecard);
    }

    public async Task<EvaluationScorecardDto> PublishScorecardAsync(
        Guid id,
        long revision,
        CancellationToken cancellationToken = default)
    {
        var scorecard = await _repository.GetScorecardAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.scorecard.not_found", $"Evaluation scorecard '{id}' was not found.");
        if (scorecard.Status.Equals("Published", StringComparison.OrdinalIgnoreCase)) return Map(scorecard);
        if (scorecard.Metrics.Count == 0)
            throw new DomainRuleViolationException("evaluation.scorecard.metrics_required", "A scorecard requires at least one metric before publication.");
        var totalWeight = scorecard.Metrics.Sum(item => item.Weight);
        if (totalWeight <= 0)
            throw new DomainRuleViolationException("evaluation.scorecard.weight_required", "The scorecard metric weights must total more than zero.");

        var published = scorecard with
        {
            Status = "Published",
            Revision = scorecard.Revision + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.UpdateScorecardAsync(published, revision, cancellationToken);
        return Map(published);
    }

    public async Task<IReadOnlyList<EvaluationRunDto>> ListRunsAsync(
        int limit = 250,
        CancellationToken cancellationToken = default)
    {
        await SynchronizeSimulationRunsAsync(cancellationToken);
        return await ListRunsInternalAsync(limit, cancellationToken);
    }

    public async Task<EvaluationRunDto> EvaluateRunAsync(
        EvaluateSimulationRunCommand command,
        CancellationToken cancellationToken = default)
    {
        var state = await _simulations.GetAsync(command.SimulationId, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.simulation.not_found", $"Simulation '{command.SimulationId}' was not found.");
        var snapshot = state.Snapshot();
        var run = snapshot.Runs.SingleOrDefault(item => item.Id == command.SourceRunId)
            ?? throw new ResourceNotFoundException("evaluation.source_run.not_found", $"Simulation run '{command.SourceRunId}' was not found.");
        return await RecordSimulationRunAsync(snapshot.Id, snapshot.Title, run, command.ScorecardId, cancellationToken);
    }

    public async Task<EvaluationRunDto> RecordSimulationRunAsync(
        Guid simulationId,
        string simulationTitle,
        SimulationRun run,
        Guid? scorecardId = null,
        CancellationToken cancellationToken = default)
    {
        var scorecard = await ResolveScorecardAsync(scorecardId, cancellationToken);
        var existing = await _repository.GetRunBySourceAsync(run.Id, scorecard.Id, cancellationToken);
        if (existing is not null) return Map(existing);

        var sourceScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["groundedness"] = run.Evaluation.Groundedness,
            ["relevance"] = run.Evaluation.Relevance,
            ["safety"] = run.Evaluation.Safety,
            ["completeness"] = CalculateCompleteness(run),
            ["execution-success"] = run.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ? 1 : 0
        };
        var id = Guid.NewGuid();
        var totalWeight = scorecard.Metrics.Sum(item => item.Weight);
        var results = scorecard.Metrics.Select(metric =>
        {
            var score = sourceScores.TryGetValue(metric.Key, out var value) ? ClampScore(value) : 0;
            return new EvaluationMetricResultState(
                Guid.NewGuid(), id, metric.Key, metric.DisplayName, score, metric.Threshold, metric.Weight,
                score >= metric.Threshold,
                BuildMetricDetail(metric, score));
        }).ToList();
        var overall = totalWeight <= 0 ? 0 : results.Sum(item => item.Score * item.Weight) / totalWeight;
        var requiredPassed = results.Where((_, index) => scorecard.Metrics[index].Required).All(item => item.Passed);
        var verdict = run.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            ? DetermineVerdict(overall, scorecard.QualityGateThreshold, requiredPassed)
            : "Failed";
        var state = new EvaluationRunState(
            id,
            simulationId,
            simulationTitle,
            run.Id,
            scorecard.Id,
            scorecard.Name,
            scorecard.Version,
            run.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : "Failed",
            verdict,
            overall,
            results,
            run.FailureReason,
            "Unreviewed",
            null,
            null,
            null,
            run.CreatedAt);
        await _repository.AddRunAsync(state, cancellationToken);
        return Map(state);
    }

    public async Task<EvaluationRunDto> ReviewRunAsync(
        Guid id,
        ReviewEvaluationRunCommand command,
        CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetRunAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.run.not_found", $"Evaluation run '{id}' was not found.");
        var allowed = new[] { "Approved", "Rejected", "NeedsChanges", "Unreviewed" };
        if (!allowed.Contains(command.Status, StringComparer.OrdinalIgnoreCase))
            throw new RequestValidationException("evaluation.review.invalid_status", "Review status must be Approved, Rejected, NeedsChanges, or Unreviewed.");
        if (string.IsNullOrWhiteSpace(command.Reviewer))
            throw new RequestValidationException("evaluation.review.reviewer_required", "A reviewer is required.");
        var reviewedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateRunReviewAsync(id, command.Status, command.Reviewer.Trim(), command.Notes?.Trim(), reviewedAt, cancellationToken);
        return Map(run with
        {
            ReviewStatus = command.Status,
            Reviewer = command.Reviewer.Trim(),
            ReviewNotes = command.Notes?.Trim(),
            ReviewedAt = reviewedAt
        });
    }

    public async Task<EvaluationComparisonDto> CompareRunsAsync(
        Guid baselineId,
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var baseline = await _repository.GetRunAsync(baselineId, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.baseline.not_found", $"Baseline evaluation '{baselineId}' was not found.");
        var candidate = await _repository.GetRunAsync(candidateId, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.candidate.not_found", $"Candidate evaluation '{candidateId}' was not found.");
        var metrics = baseline.Metrics.Select(metric =>
        {
            var candidateMetric = candidate.Metrics.FirstOrDefault(item => item.Key.Equals(metric.Key, StringComparison.OrdinalIgnoreCase));
            var candidateScore = candidateMetric?.Score ?? 0;
            var delta = candidateScore - metric.Score;
            return new EvaluationComparisonMetricDto(metric.Key, metric.DisplayName, metric.Score, candidateScore, delta,
                delta > .001 ? "Improved" : delta < -.001 ? "Regressed" : "Unchanged");
        }).ToList();
        var overallDelta = candidate.OverallScore - baseline.OverallScore;
        var findings = metrics.Where(item => item.Direction != "Unchanged")
            .OrderBy(item => item.Delta)
            .Select(item => $"{item.DisplayName} {item.Direction.ToLowerInvariant()} by {Math.Abs(item.Delta):P1}.")
            .ToList();
        if (findings.Count == 0) findings.Add("No material quality change was detected.");
        return new EvaluationComparisonDto(
            Map(baseline),
            Map(candidate),
            overallDelta,
            overallDelta > .01 ? "Improved" : overallDelta < -.01 ? "Regression" : "Equivalent",
            metrics,
            findings);
    }

    public async Task<IReadOnlyList<EvaluationTestCaseDto>> ListTestCasesAsync(CancellationToken cancellationToken = default)
        => (await _repository.ListTestCasesAsync(cancellationToken)).Select(Map).ToList();

    public async Task<EvaluationTestCaseDto> CreateTestCaseAsync(
        CreateEvaluationTestCaseCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new RequestValidationException("evaluation.test_case.name_required", "Test case name is required.");
        var simulation = await _simulations.GetAsync(command.SimulationId, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.simulation.not_found", $"Simulation '{command.SimulationId}' was not found.");
        if (simulation.FindRun(command.SourceRunId) is null)
            throw new ResourceNotFoundException("evaluation.source_run.not_found", $"Simulation run '{command.SourceRunId}' was not found.");
        if (command.ScorecardId.HasValue && await _repository.GetScorecardAsync(command.ScorecardId.Value, cancellationToken) is null)
            throw new ResourceNotFoundException("evaluation.scorecard.not_found", $"Evaluation scorecard '{command.ScorecardId}' was not found.");
        var expected = NormalizeExpectedVerdict(command.ExpectedVerdict);
        var now = DateTimeOffset.UtcNow;
        var state = new EvaluationTestCaseState(
            Guid.NewGuid(), command.Name.Trim(), command.Description.Trim(), command.SimulationId, command.SourceRunId,
            command.ScorecardId, expected, command.Tags?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
            "Active", 1, now, now);
        await _repository.AddTestCaseAsync(state, cancellationToken);
        return Map(state);
    }

    public async Task<EvaluationBatchDto> RunBatchAsync(
        RunEvaluationBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new RequestValidationException("evaluation.batch.name_required", "Batch name is required.");
        if (command.TestCaseIds.Count == 0)
            throw new RequestValidationException("evaluation.batch.test_cases_required", "Select at least one evaluation test case.");
        var scorecard = await ResolveScorecardAsync(command.ScorecardId, cancellationToken);
        var testCases = new List<EvaluationTestCaseState>();
        foreach (var id in command.TestCaseIds.Distinct())
        {
            var testCase = await _repository.GetTestCaseAsync(id, cancellationToken)
                ?? throw new ResourceNotFoundException("evaluation.test_case.not_found", $"Evaluation test case '{id}' was not found.");
            testCases.Add(testCase);
        }

        var batchId = Guid.NewGuid();
        var items = new List<EvaluationBatchItemState>();
        foreach (var testCase in testCases)
        {
            try
            {
                var evaluated = await EvaluateRunAsync(new EvaluateSimulationRunCommand(
                    testCase.SimulationId,
                    testCase.SourceRunId,
                    testCase.ScorecardId ?? scorecard.Id), cancellationToken);
                var passed = evaluated.Verdict.Equals(testCase.ExpectedVerdict, StringComparison.OrdinalIgnoreCase);
                items.Add(new EvaluationBatchItemState(
                    Guid.NewGuid(), batchId, testCase.Id, testCase.Name, evaluated.Id, "Completed", evaluated.Verdict,
                    testCase.ExpectedVerdict, passed,
                    passed ? "Actual verdict matched the expected quality outcome." : $"Expected {testCase.ExpectedVerdict}, received {evaluated.Verdict}."));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                items.Add(new EvaluationBatchItemState(
                    Guid.NewGuid(), batchId, testCase.Id, testCase.Name, null, "Failed", "Error", testCase.ExpectedVerdict,
                    false, exception.Message));
            }
        }
        var now = DateTimeOffset.UtcNow;
        var batch = new EvaluationBatchState(batchId, command.Name.Trim(), scorecard.Id, scorecard.Name, "Completed", items, now, DateTimeOffset.UtcNow);
        await _repository.AddBatchAsync(batch, cancellationToken);
        return Map(batch);
    }

    private async Task SynchronizeSimulationRunsAsync(CancellationToken cancellationToken)
    {
        var scorecard = await ResolveScorecardAsync(null, cancellationToken);
        var simulations = await _simulations.ListAsync(cancellationToken);
        foreach (var simulation in simulations)
        {
            var snapshot = simulation.Snapshot();
            foreach (var run in snapshot.Runs)
                await RecordSimulationRunAsync(snapshot.Id, snapshot.Title, run, scorecard.Id, cancellationToken);
        }
    }

    private async Task<EvaluationScorecardState> ResolveScorecardAsync(Guid? id, CancellationToken cancellationToken)
    {
        await EnsureDefaultScorecardAsync(cancellationToken);
        var scorecards = await _repository.ListScorecardsAsync(cancellationToken);
        var scorecard = id.HasValue
            ? scorecards.SingleOrDefault(item => item.Id == id.Value)
            : scorecards.FirstOrDefault(item => item.IsDefault && item.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))
              ?? scorecards.FirstOrDefault(item => item.Status.Equals("Published", StringComparison.OrdinalIgnoreCase));
        return scorecard ?? throw new ResourceNotFoundException("evaluation.scorecard.not_found", "No published evaluation scorecard is available.");
    }

    private async Task EnsureDefaultScorecardAsync(CancellationToken cancellationToken)
    {
        await _repository.BackfillLegacyScorecardsAsync(cancellationToken);
        var current = await _repository.ListScorecardsAsync(cancellationToken);
        if (current.Any(item => item.IsDefault && item.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))) return;
        await SeedGate.WaitAsync(cancellationToken);
        try
        {
            await _repository.BackfillLegacyScorecardsAsync(cancellationToken);
            current = await _repository.ListScorecardsAsync(cancellationToken);
            if (current.Any(item => item.IsDefault && item.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))) return;
            var now = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid();
            var scorecard = new EvaluationScorecardState(
                id,
                "ConvoLab Response Quality",
                "Default production-oriented scorecard for grounded, relevant, safe, and complete conversational responses.",
                "Published",
                "1.0",
                .85,
                true,
                1,
                DefaultMetricCommands().Select(metric => new EvaluationMetricDefinitionState(
                    Guid.NewGuid(), id, NormalizeKey(metric.Key), metric.DisplayName, metric.Description, metric.Weight,
                    metric.Threshold, metric.Required)).ToList(),
                now,
                now);
            await _repository.AddScorecardAsync(scorecard, cancellationToken);
        }
        finally
        {
            SeedGate.Release();
        }
    }

    private async Task<IReadOnlyList<EvaluationRunDto>> ListRunsInternalAsync(int limit, CancellationToken cancellationToken)
        => (await _repository.ListRunsAsync(Math.Clamp(limit, 1, 1000), cancellationToken)).Select(Map).ToList();

    private static IReadOnlyList<CreateEvaluationMetricCommand> DefaultMetricCommands() =>
    [
        new("groundedness", "Groundedness", "Response is supported by governed knowledge and citations.", .35, .90, true),
        new("relevance", "Relevance", "Response directly addresses the customer request.", .30, .85, true),
        new("safety", "Safety", "Response avoids unsafe or disallowed output.", .20, .95, true),
        new("completeness", "Completeness", "Response is sufficiently detailed and actionable.", .15, .80, false)
    ];

    private static void ValidateScorecard(CreateEvaluationScorecardCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new RequestValidationException("evaluation.scorecard.name_required", "Scorecard name is required.");
        if (command.QualityGateThreshold is < 0 or > 1)
            throw new RequestValidationException("evaluation.scorecard.threshold_invalid", "Quality gate threshold must be between 0 and 1.");
        if (command.Metrics is not null && command.Metrics.Any(metric => metric.Threshold is < 0 or > 1 || metric.Weight < 0))
            throw new RequestValidationException("evaluation.scorecard.metric_invalid", "Metric thresholds must be between 0 and 1 and weights cannot be negative.");
    }

    private static double CalculateCompleteness(SimulationRun run)
    {
        if (!run.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return 0;
        var timelineComplete = run.Timeline.Count(item => item.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) >= 6;
        var hasKnowledge = run.KnowledgePackage.Citations.Count > 0;
        var hasUsage = run.Metrics is not null && run.Metrics.OutputTokens > 20;
        return (timelineComplete ? .35 : .15) + (hasKnowledge ? .35 : .15) + (hasUsage ? .30 : .15);
    }

    private static string DetermineVerdict(double overall, double gate, bool requiredPassed)
    {
        if (requiredPassed && overall >= gate) return "Passed";
        if (overall >= Math.Max(0, gate - .10)) return "Review";
        return "Failed";
    }

    private static string BuildMetricDetail(EvaluationMetricDefinitionState metric, double score)
        => score >= metric.Threshold
            ? $"Passed the {metric.Threshold:P0} threshold."
            : $"Below the {metric.Threshold:P0} threshold by {metric.Threshold - score:P1}.";

    private static string NormalizeExpectedVerdict(string verdict)
    {
        var normalized = string.IsNullOrWhiteSpace(verdict) ? "Passed" : verdict.Trim();
        var allowed = new[] { "Passed", "Review", "Failed" };
        return allowed.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new RequestValidationException("evaluation.test_case.verdict_invalid", "Expected verdict must be Passed, Review, or Failed.");
    }

    private static string NormalizeKey(string key)
        => string.Join('-', (key ?? string.Empty).Trim().ToLowerInvariant().Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries));

    private static double ClampScore(double score) => Math.Clamp(score, 0, 1);

    private static EvaluationScorecardDto Map(EvaluationScorecardState state) => new(
        state.Id, state.Name, state.Description, state.Status, state.Version, state.QualityGateThreshold, state.IsDefault,
        state.Revision,
        state.Metrics.Select(metric => new EvaluationMetricDefinitionDto(metric.Id, metric.Key, metric.DisplayName, metric.Description, metric.Weight, metric.Threshold, metric.Required)).ToList(),
        state.CreatedAt, state.UpdatedAt);

    private static EvaluationRunDto Map(EvaluationRunState state) => new(
        state.Id, state.SimulationId, state.SimulationTitle, state.SourceRunId, state.ScorecardId, state.ScorecardName,
        state.ScorecardVersion, state.Status, state.Verdict, state.OverallScore,
        state.Metrics.Select(metric => new EvaluationMetricResultDto(metric.Id, metric.Key, metric.DisplayName, metric.Score, metric.Threshold, metric.Weight, metric.Passed, metric.Detail)).ToList(),
        state.FailureReason, state.ReviewStatus, state.ReviewNotes, state.Reviewer, state.ReviewedAt, state.CreatedAt);

    private static EvaluationTestCaseDto Map(EvaluationTestCaseState state) => new(
        state.Id, state.Name, state.Description, state.SimulationId, state.SourceRunId, state.ScorecardId,
        state.ExpectedVerdict, state.Tags, state.Status, state.Revision, state.CreatedAt, state.UpdatedAt);

    private static EvaluationBatchDto Map(EvaluationBatchState state)
    {
        var passed = state.Items.Count(item => item.Passed);
        return new EvaluationBatchDto(
            state.Id, state.Name, state.ScorecardId, state.ScorecardName, state.Status, state.Items.Count, passed,
            state.Items.Count == 0 ? 0 : passed / (double)state.Items.Count,
            state.Items.Select(item => new EvaluationBatchItemDto(item.Id, item.TestCaseId, item.TestCaseName, item.EvaluationRunId,
                item.Status, item.ActualVerdict, item.ExpectedVerdict, item.Passed, item.Detail)).ToList(),
            state.StartedAt, state.CompletedAt);
    }
}
