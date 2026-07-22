using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EfEvaluationStudioRepository(ApplicationDbContext db) : IEvaluationStudioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task BackfillLegacyScorecardsAsync(CancellationToken cancellationToken = default)
    {
        var scorecards = await db.EvaluationScorecards.ToListAsync(cancellationToken);
        if (scorecards.Count == 0) return;

        var scorecardIds = scorecards.Select(item => item.Id).ToList();
        var scorecardsWithMetrics = await db.EvaluationMetricDefinitions
            .AsNoTracking()
            .Where(item => scorecardIds.Contains(item.ScorecardId))
            .Select(item => item.ScorecardId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var populated = scorecardsWithMetrics.ToHashSet();

        foreach (var scorecard in scorecards)
        {
            scorecard.Status = string.IsNullOrWhiteSpace(scorecard.Status) ? "Published" : scorecard.Status;
            scorecard.Version = string.IsNullOrWhiteSpace(scorecard.Version) ? "1.0" : scorecard.Version;
            scorecard.QualityGateThreshold = scorecard.QualityGateThreshold > 0
                ? scorecard.QualityGateThreshold
                : scorecard.MinimumOverallScore;
            scorecard.Revision = Math.Max(1, scorecard.Revision);

            if (populated.Contains(scorecard.Id)) continue;
            db.EvaluationMetricDefinitions.AddRange(
                LegacyMetric(scorecard.Id, "groundedness", "Groundedness", .40, scorecard.MinimumGroundedness),
                LegacyMetric(scorecard.Id, "relevance", "Relevance", .35, scorecard.MinimumRelevance),
                LegacyMetric(scorecard.Id, "safety", "Safety", .25, scorecard.MinimumSafety));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationScorecardState>> ListScorecardsAsync(CancellationToken cancellationToken = default)
    {
        var records = await db.EvaluationScorecards.AsNoTracking().OrderByDescending(item => item.IsDefault).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var ids = records.Select(item => item.Id).ToList();
        var metrics = await db.EvaluationMetricDefinitions.AsNoTracking().Where(item => ids.Contains(item.ScorecardId)).OrderBy(item => item.DisplayName).ToListAsync(cancellationToken);
        return records.Select(record => Map(record, metrics.Where(item => item.ScorecardId == record.Id).ToList())).ToList();
    }

    public async Task<EvaluationScorecardState?> GetScorecardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.EvaluationScorecards.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is null) return null;
        var metrics = await db.EvaluationMetricDefinitions.AsNoTracking().Where(item => item.ScorecardId == id).OrderBy(item => item.DisplayName).ToListAsync(cancellationToken);
        return Map(record, metrics);
    }

    public async Task AddScorecardAsync(EvaluationScorecardState scorecard, CancellationToken cancellationToken = default)
    {
        db.EvaluationScorecards.Add(MapRecord(scorecard));
        db.EvaluationMetricDefinitions.AddRange(scorecard.Metrics.Select(MapRecord));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateScorecardAsync(EvaluationScorecardState scorecard, long expectedRevision, CancellationToken cancellationToken = default)
    {
        var record = await db.EvaluationScorecards.SingleOrDefaultAsync(item => item.Id == scorecard.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.scorecard.not_found", $"Evaluation scorecard '{scorecard.Id}' was not found.");
        if (record.Revision != expectedRevision)
            throw new ConcurrencyConflictException("evaluation scorecard", scorecard.Id);
        record.Name = scorecard.Name;
        record.Description = scorecard.Description;
        record.Status = scorecard.Status;
        record.Version = scorecard.Version;
        record.QualityGateThreshold = scorecard.QualityGateThreshold;
        record.IsDefault = scorecard.IsDefault;
        record.Revision = scorecard.Revision;
        record.UpdatedAt = scorecard.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationRunState>> ListRunsAsync(int limit = 250, CancellationToken cancellationToken = default)
    {
        var records = (await db.EvaluationRuns.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAt).Take(limit).ToList();
        var ids = records.Select(item => item.Id).ToList();
        var metrics = await db.EvaluationMetricResults.AsNoTracking().Where(item => ids.Contains(item.EvaluationRunId)).OrderBy(item => item.DisplayName).ToListAsync(cancellationToken);
        return records.Select(record => Map(record, metrics.Where(item => item.EvaluationRunId == record.Id).ToList())).ToList();
    }

    public async Task<EvaluationRunState?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.EvaluationRuns.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is null) return null;
        var metrics = await db.EvaluationMetricResults.AsNoTracking().Where(item => item.EvaluationRunId == id).OrderBy(item => item.DisplayName).ToListAsync(cancellationToken);
        return Map(record, metrics);
    }

    public async Task<EvaluationRunState?> GetRunBySourceAsync(Guid sourceRunId, Guid scorecardId, CancellationToken cancellationToken = default)
    {
        var record = await db.EvaluationRuns.AsNoTracking().SingleOrDefaultAsync(item => item.SourceRunId == sourceRunId && item.ScorecardId == scorecardId, cancellationToken);
        if (record is null) return null;
        var metrics = await db.EvaluationMetricResults.AsNoTracking().Where(item => item.EvaluationRunId == record.Id).OrderBy(item => item.DisplayName).ToListAsync(cancellationToken);
        return Map(record, metrics);
    }

    public async Task AddRunAsync(EvaluationRunState run, CancellationToken cancellationToken = default)
    {
        db.EvaluationRuns.Add(MapRecord(run));
        db.EvaluationMetricResults.AddRange(run.Metrics.Select(MapRecord));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var exists = await db.EvaluationRuns.AsNoTracking().AnyAsync(item => item.SourceRunId == run.SourceRunId && item.ScorecardId == run.ScorecardId, cancellationToken);
            if (!exists) throw;
            db.ChangeTracker.Clear();
        }
    }

    public async Task UpdateRunReviewAsync(Guid id, string status, string reviewer, string? notes, DateTimeOffset reviewedAt, CancellationToken cancellationToken = default)
    {
        var record = await db.EvaluationRuns.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("evaluation.run.not_found", $"Evaluation run '{id}' was not found.");
        record.ReviewStatus = status;
        record.Reviewer = reviewer;
        record.ReviewNotes = notes;
        record.ReviewedAt = reviewedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationTestCaseState>> ListTestCasesAsync(CancellationToken cancellationToken = default)
        => (await db.EvaluationTestCases.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAt).Select(Map).ToList();

    public async Task<EvaluationTestCaseState?> GetTestCaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.EvaluationTestCases.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Map(record);
    }

    public async Task AddTestCaseAsync(EvaluationTestCaseState testCase, CancellationToken cancellationToken = default)
    {
        db.EvaluationTestCases.Add(MapRecord(testCase));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationBatchState>> ListBatchesAsync(int limit = 25, CancellationToken cancellationToken = default)
    {
        var records = (await db.EvaluationBatches.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.StartedAt).Take(limit).ToList();
        var ids = records.Select(item => item.Id).ToList();
        var items = await db.EvaluationBatchItems.AsNoTracking().Where(item => ids.Contains(item.BatchId)).OrderBy(item => item.TestCaseName).ToListAsync(cancellationToken);
        return records.Select(record => Map(record, items.Where(item => item.BatchId == record.Id).ToList())).ToList();
    }

    public async Task AddBatchAsync(EvaluationBatchState batch, CancellationToken cancellationToken = default)
    {
        db.EvaluationBatches.Add(MapRecord(batch));
        db.EvaluationBatchItems.AddRange(batch.Items.Select(MapRecord));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static EvaluationScorecardState Map(EvaluationScorecardRecord record, IReadOnlyList<EvaluationMetricDefinitionRecord> metrics)
        => new(record.Id, record.Name, record.Description, record.Status, record.Version, record.QualityGateThreshold, record.IsDefault,
            record.Revision, metrics.Select(Map).ToList(), record.CreatedAt, record.UpdatedAt);

    private static EvaluationMetricDefinitionState Map(EvaluationMetricDefinitionRecord record)
        => new(record.Id, record.ScorecardId, record.Key, record.DisplayName, record.Description, record.Weight, record.Threshold, record.Required);

    private static EvaluationRunState Map(EvaluationRunRecord record, IReadOnlyList<EvaluationMetricResultRecord> metrics)
        => new(record.Id, record.SimulationId, record.SimulationTitle, record.SourceRunId, record.ScorecardId, record.ScorecardName,
            record.ScorecardVersion, record.Status, record.Verdict, record.OverallScore, metrics.Select(Map).ToList(), record.FailureReason,
            record.ReviewStatus, record.ReviewNotes, record.Reviewer, record.ReviewedAt, record.CreatedAt);

    private static EvaluationMetricResultState Map(EvaluationMetricResultRecord record)
        => new(record.Id, record.EvaluationRunId, record.Key, record.DisplayName, record.Score, record.Threshold, record.Weight, record.Passed, record.Detail);

    private static EvaluationTestCaseState Map(EvaluationTestCaseRecord record)
        => new(record.Id, record.Name, record.Description, record.SimulationId, record.SourceRunId, record.ScorecardId,
            record.ExpectedVerdict, JsonSerializer.Deserialize<List<string>>(record.TagsJson, JsonOptions) ?? [], record.Status,
            record.Revision, record.CreatedAt, record.UpdatedAt);

    private static EvaluationBatchState Map(EvaluationBatchRecord record, IReadOnlyList<EvaluationBatchItemRecord> items)
        => new(record.Id, record.Name, record.ScorecardId, record.ScorecardName, record.Status, items.Select(Map).ToList(), record.StartedAt, record.CompletedAt);

    private static EvaluationBatchItemState Map(EvaluationBatchItemRecord record)
        => new(record.Id, record.BatchId, record.TestCaseId, record.TestCaseName, record.EvaluationRunId, record.Status,
            record.ActualVerdict, record.ExpectedVerdict, record.Passed, record.Detail);

    private static EvaluationScorecardRecord MapRecord(EvaluationScorecardState state) => new()
    {
        Id = state.Id, Name = state.Name, Description = state.Description, Status = state.Status, Version = state.Version,
        QualityGateThreshold = state.QualityGateThreshold, IsDefault = state.IsDefault, Revision = state.Revision,
        MinimumGroundedness = MetricThreshold(state, "groundedness", .90),
        MinimumRelevance = MetricThreshold(state, "relevance", .85),
        MinimumSafety = MetricThreshold(state, "safety", .95),
        MinimumOverallScore = state.QualityGateThreshold,
        FailureAction = "Review",
        CreatedAt = state.CreatedAt, UpdatedAt = state.UpdatedAt
    };

    private static double MetricThreshold(EvaluationScorecardState state, string key, double fallback)
        => state.Metrics.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Threshold ?? fallback;

    private static EvaluationMetricDefinitionRecord LegacyMetric(
        Guid scorecardId,
        string key,
        string displayName,
        double weight,
        double threshold) => new()
        {
            Id = Guid.NewGuid(),
            ScorecardId = scorecardId,
            Key = key,
            DisplayName = displayName,
            Description = $"Migrated {displayName.ToLowerInvariant()} quality gate.",
            Weight = weight,
            Threshold = threshold,
            Required = true
        };

    private static EvaluationMetricDefinitionRecord MapRecord(EvaluationMetricDefinitionState state) => new()
    {
        Id = state.Id, ScorecardId = state.ScorecardId, Key = state.Key, DisplayName = state.DisplayName,
        Description = state.Description, Weight = state.Weight, Threshold = state.Threshold, Required = state.Required
    };

    private static EvaluationRunRecord MapRecord(EvaluationRunState state) => new()
    {
        Id = state.Id, SimulationId = state.SimulationId, SimulationTitle = state.SimulationTitle, SourceRunId = state.SourceRunId,
        ScorecardId = state.ScorecardId, ScorecardName = state.ScorecardName, ScorecardVersion = state.ScorecardVersion,
        Status = state.Status, Verdict = state.Verdict, OverallScore = state.OverallScore, FailureReason = state.FailureReason,
        ReviewStatus = state.ReviewStatus, ReviewNotes = state.ReviewNotes, Reviewer = state.Reviewer,
        ReviewedAt = state.ReviewedAt, CreatedAt = state.CreatedAt
    };

    private static EvaluationMetricResultRecord MapRecord(EvaluationMetricResultState state) => new()
    {
        Id = state.Id, EvaluationRunId = state.EvaluationRunId, Key = state.Key, DisplayName = state.DisplayName,
        Score = state.Score, Threshold = state.Threshold, Weight = state.Weight, Passed = state.Passed, Detail = state.Detail
    };

    private static EvaluationTestCaseRecord MapRecord(EvaluationTestCaseState state) => new()
    {
        Id = state.Id, Name = state.Name, Description = state.Description, SimulationId = state.SimulationId,
        SourceRunId = state.SourceRunId, ScorecardId = state.ScorecardId, ExpectedVerdict = state.ExpectedVerdict,
        TagsJson = JsonSerializer.Serialize(state.Tags, JsonOptions), Status = state.Status, Revision = state.Revision,
        CreatedAt = state.CreatedAt, UpdatedAt = state.UpdatedAt
    };

    private static EvaluationBatchRecord MapRecord(EvaluationBatchState state) => new()
    {
        Id = state.Id, Name = state.Name, ScorecardId = state.ScorecardId, ScorecardName = state.ScorecardName,
        Status = state.Status, StartedAt = state.StartedAt, CompletedAt = state.CompletedAt
    };

    private static EvaluationBatchItemRecord MapRecord(EvaluationBatchItemState state) => new()
    {
        Id = state.Id, BatchId = state.BatchId, TestCaseId = state.TestCaseId, TestCaseName = state.TestCaseName,
        EvaluationRunId = state.EvaluationRunId, Status = state.Status, ActualVerdict = state.ActualVerdict,
        ExpectedVerdict = state.ExpectedVerdict, Passed = state.Passed, Detail = state.Detail
    };
}
