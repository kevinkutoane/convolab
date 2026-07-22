using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.EvaluationStudio;

public sealed class LegacyEvaluationStudioService : ILegacyEvaluationStudioService
{
    private readonly IConversationSimulationStore _simulations;
    private readonly IEvaluationStudioConfiguration _configuration;
    private readonly IEvaluationScorecardRepository _scorecards;
    private readonly IUnitOfWork _unitOfWork;

    public LegacyEvaluationStudioService(
        IConversationSimulationStore simulations,
        IEvaluationStudioConfiguration configuration,
        IEvaluationScorecardRepository scorecards,
        IUnitOfWork unitOfWork)
    {
        _simulations = simulations;
        _configuration = configuration;
        _scorecards = scorecards;
        _unitOfWork = unitOfWork;
    }

    public async Task<LegacyEvaluationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var runs = await ListRunsAsync(250, cancellationToken);
        var policy = _configuration.GetPolicy();
        var passing = runs.Count(run => run.Passed);
        var failing = runs.Count - passing;
        var now = DateTimeOffset.UtcNow;

        var metrics = new[]
        {
            Summarize("Groundedness", runs.Select(run => run.Groundedness), policy.MinimumGroundedness),
            Summarize("Relevance", runs.Select(run => run.Relevance), policy.MinimumRelevance),
            Summarize("Safety", runs.Select(run => run.Safety), policy.MinimumSafety),
            Summarize("Overall", runs.Select(run => run.OverallScore), policy.MinimumOverallScore)
        };

        var trend = Enumerable.Range(0, 7)
            .Select(offset => DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(offset - 6)))
            .Select(date =>
            {
                var dayRuns = runs
                    .Where(run => DateOnly.FromDateTime(run.CreatedAt.UtcDateTime) == date)
                    .ToList();
                return new LegacyEvaluationDailyTrendDto(
                    date,
                    dayRuns.Count,
                    dayRuns.Count == 0 ? 0d : dayRuns.Average(run => run.OverallScore),
                    dayRuns.Count == 0 ? 0d : dayRuns.Count(run => run.Passed) / (double)dayRuns.Count);
            })
            .ToList();

        return new LegacyEvaluationOverviewDto(
            runs.Count,
            runs.Count,
            passing,
            failing,
            runs.Count == 0 ? 0d : passing / (double)runs.Count,
            runs.Count == 0 ? 0d : runs.Average(run => run.OverallScore),
            policy,
            metrics,
            trend,
            runs.Take(30).ToList(),
            now);
    }

    public async Task<IReadOnlyList<LegacyEvaluationRunDto>> ListRunsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var policy = _configuration.GetPolicy();
        var simulations = await _simulations.ListAsync(cancellationToken);
        return simulations
            .Select(state => state.Snapshot())
            .SelectMany(simulation => simulation.Runs.Select(run => Map(simulation, run, policy)))
            .OrderByDescending(run => run.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    public async Task<IReadOnlyList<LegacyEvaluationScorecardDto>> ListScorecardsAsync(
        CancellationToken cancellationToken = default)
        => (await _scorecards.ListAsync(cancellationToken)).Select(MapScorecard).ToList();

    public async Task<LegacyEvaluationScorecardDto> CreateScorecardAsync(
        CreateLegacyEvaluationScorecardCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateScorecard(command);
        var name = command.Name?.Trim() ?? string.Empty;
        if (await _scorecards.NameExistsAsync(name, cancellationToken))
            throw new RequestValidationException(
                "evaluation.scorecard.name_conflict",
                "A scorecard with this name already exists.",
                new Dictionary<string, string[]> { ["name"] = ["Scorecard names must be unique."] });

        var now = DateTimeOffset.UtcNow;
        var scorecard = new LegacyEvaluationScorecardState(
            Guid.NewGuid(),
            name,
            command.Description?.Trim() ?? string.Empty,
            command.MinimumGroundedness,
            command.MinimumRelevance,
            command.MinimumSafety,
            command.MinimumOverallScore,
            command.FailureAction?.Trim() ?? string.Empty,
            now,
            now);

        await _scorecards.AddAsync(scorecard, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapScorecard(scorecard);
    }

    public async Task<LegacyEvaluationPreviewDto> PreviewAsync(
        LegacyEvaluationPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePreview(command);

        var configured = command.ScorecardId.HasValue
            ? await GetScorecardPolicyAsync(command.ScorecardId.Value, cancellationToken)
            : _configuration.GetPolicy();
        var policy = new LegacyEvaluationPolicyDto(
            command.MinimumGroundedness ?? configured.MinimumGroundedness,
            command.MinimumRelevance ?? configured.MinimumRelevance,
            command.MinimumSafety ?? configured.MinimumSafety,
            command.MinimumOverallScore ?? configured.MinimumOverallScore,
            configured.FailureAction);

        return Evaluate(
            command.Groundedness,
            command.Relevance,
            command.Safety,
            policy);
    }

    private async Task<LegacyEvaluationPolicyDto> GetScorecardPolicyAsync(Guid id, CancellationToken cancellationToken)
    {
        var scorecard = await _scorecards.GetAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException(
                "evaluation.scorecard.not_found",
                $"Evaluation scorecard '{id}' was not found.");
        return new LegacyEvaluationPolicyDto(
            scorecard.MinimumGroundedness,
            scorecard.MinimumRelevance,
            scorecard.MinimumSafety,
            scorecard.MinimumOverallScore,
            scorecard.FailureAction);
    }

    private static LegacyEvaluationRunDto Map(
        SimulationConversation simulation,
        SimulationRun run,
        LegacyEvaluationPolicyDto policy)
    {
        var preview = Evaluate(
            Clamp(run.Evaluation.Groundedness),
            Clamp(run.Evaluation.Relevance),
            Clamp(run.Evaluation.Safety),
            policy);

        return new LegacyEvaluationRunDto(
            simulation.Id,
            simulation.Title,
            run.Id,
            run.ExecutionPlan?.Provider ?? "Not planned",
            run.ExecutionPlan?.Model ?? "Not planned",
            run.Status,
            preview.Groundedness,
            preview.Relevance,
            preview.Safety,
            preview.OverallScore,
            preview.Verdict,
            preview.Passed,
            preview.FailedGates,
            run.CreatedAt);
    }

    private static LegacyEvaluationPreviewDto Evaluate(
        double groundedness,
        double relevance,
        double safety,
        LegacyEvaluationPolicyDto policy)
    {
        var overall = Math.Round((groundedness * .4) + (relevance * .35) + (safety * .25), 4);
        var decisions = new[]
        {
            Decision("Groundedness", groundedness, policy.MinimumGroundedness),
            Decision("Relevance", relevance, policy.MinimumRelevance),
            Decision("Safety", safety, policy.MinimumSafety),
            Decision("Overall", overall, policy.MinimumOverallScore)
        };
        var failed = decisions
            .Where(decision => decision.Status == "Failed")
            .Select(decision => decision.Name)
            .ToList();
        var passed = failed.Count == 0;
        return new LegacyEvaluationPreviewDto(
            groundedness,
            relevance,
            safety,
            overall,
            passed,
            passed ? "Pass" : policy.FailureAction,
            failed,
            decisions);
    }

    private static LegacyEvaluationGateDecisionDto Decision(string name, double score, double threshold)
        => new(name, score, threshold, score >= threshold ? "Passed" : "Failed");

    private static LegacyEvaluationMetricSummaryDto Summarize(
        string name,
        IEnumerable<double> source,
        double threshold)
    {
        var values = source.ToList();
        if (values.Count == 0) return new(name, 0d, 0d, 0d, threshold, 0, 0);

        return new LegacyEvaluationMetricSummaryDto(
            name,
            values.Average(),
            values.Min(),
            values.Max(),
            threshold,
            values.Count(value => value >= threshold),
            values.Count(value => value < threshold));
    }

    private static void ValidatePreview(LegacyEvaluationPreviewCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateScore(command.Groundedness, "groundedness", errors);
        ValidateScore(command.Relevance, "relevance", errors);
        ValidateScore(command.Safety, "safety", errors);
        ValidateOptionalScore(command.MinimumGroundedness, "minimumGroundedness", errors);
        ValidateOptionalScore(command.MinimumRelevance, "minimumRelevance", errors);
        ValidateOptionalScore(command.MinimumSafety, "minimumSafety", errors);
        ValidateOptionalScore(command.MinimumOverallScore, "minimumOverallScore", errors);

        if (errors.Count > 0)
            throw new RequestValidationException(
                "evaluation.preview.invalid",
                "The evaluation preview request is invalid.",
                errors);
    }

    private static void ValidateScorecard(CreateLegacyEvaluationScorecardCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Name))
            errors["name"] = ["A scorecard name is required."];
        else if (command.Name.Trim().Length > 120)
            errors["name"] = ["The scorecard name cannot exceed 120 characters."];
        if (command.Description?.Trim().Length > 500)
            errors["description"] = ["The description cannot exceed 500 characters."];
        if (string.IsNullOrWhiteSpace(command.FailureAction))
            errors["failureAction"] = ["A failure action is required."];
        else if (command.FailureAction.Trim().Length > 80)
            errors["failureAction"] = ["The failure action cannot exceed 80 characters."];

        ValidateScore(command.MinimumGroundedness, "minimumGroundedness", errors);
        ValidateScore(command.MinimumRelevance, "minimumRelevance", errors);
        ValidateScore(command.MinimumSafety, "minimumSafety", errors);
        ValidateScore(command.MinimumOverallScore, "minimumOverallScore", errors);

        if (errors.Count > 0)
            throw new RequestValidationException(
                "evaluation.scorecard.invalid",
                "The evaluation scorecard is invalid.",
                errors);
    }

    private static LegacyEvaluationScorecardDto MapScorecard(LegacyEvaluationScorecardState scorecard)
        => new(
            scorecard.Id,
            scorecard.Name,
            scorecard.Description,
            scorecard.MinimumGroundedness,
            scorecard.MinimumRelevance,
            scorecard.MinimumSafety,
            scorecard.MinimumOverallScore,
            scorecard.FailureAction,
            scorecard.CreatedAt,
            scorecard.UpdatedAt);

    private static void ValidateScore(double value, string key, IDictionary<string, string[]> errors)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < 0d or > 1d)
            errors[key] = ["The score must be between 0 and 1."];
    }

    private static void ValidateOptionalScore(
        double? value,
        string key,
        IDictionary<string, string[]> errors)
    {
        if (value.HasValue) ValidateScore(value.Value, key, errors);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0d, 1d);
}
