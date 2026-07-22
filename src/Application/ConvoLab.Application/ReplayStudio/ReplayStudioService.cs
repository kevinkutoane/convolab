using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.ReplayStudio;

public sealed class ReplayStudioService(
    IReplayStudioRepository repository,
    IConversationSimulationStore simulations,
    IConversationSimulationService simulationService) : IReplayStudioService
{
    public async Task<ReplayOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeExistingReplaysAsync(cancellationToken);
        var experiments = await BuildExperimentSummariesAsync(cancellationToken);
        var details = new List<ReplayExperimentDetailDto>();
        foreach (var experiment in experiments)
            details.Add(await GetExperimentAsync(experiment.Id, cancellationToken));

        var candidates = details.SelectMany(item => item.Candidates).ToList();
        var qualityDeltas = candidates.Select(item => item.Comparison.QualityDelta).ToList();
        var latencyDeltas = candidates.Select(item => item.Comparison.DurationDeltaMs).ToList();
        var costDeltas = candidates.Select(item => item.Comparison.CostDelta).ToList();
        var metrics = new ReplayMetricsDto(
            experiments.Count,
            experiments.Count(item => item.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)),
            candidates.Count,
            candidates.Count(item => item.Comparison.Outcome is "Improved" or "Efficient"),
            candidates.Count(item => item.Comparison.Outcome == "Regression"),
            qualityDeltas.Count == 0 ? 0 : qualityDeltas.Average(),
            latencyDeltas.Count == 0 ? 0 : latencyDeltas.Average(),
            costDeltas.Count == 0 ? 0 : costDeltas.Average(),
            candidates.Select(item => item.Snapshot.Currency).FirstOrDefault() ?? "ZAR");

        return new ReplayOverviewDto(
            metrics,
            experiments.Take(12).ToList(),
            (await ListSourcesInternalAsync(cancellationToken)).Take(30).ToList(),
            await simulationService.GetOptionsAsync(cancellationToken),
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<ReplaySourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeExistingReplaysAsync(cancellationToken);
        return await ListSourcesInternalAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReplayExperimentSummaryDto>> ListExperimentsAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeExistingReplaysAsync(cancellationToken);
        return await BuildExperimentSummariesAsync(cancellationToken);
    }

    public async Task<ReplayExperimentDetailDto> GetExperimentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var experiment = await repository.GetExperimentAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.experiment.not_found", $"Replay experiment '{id}' was not found.");
        var simulation = await simulations.GetAsync(experiment.SimulationId, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.simulation.not_found", $"Simulation '{experiment.SimulationId}' was not found.");
        var source = simulation.FindRun(experiment.SourceRunId)
            ?? throw new ResourceNotFoundException("replay.source.not_found", $"Source run '{experiment.SourceRunId}' was not found.");
        var baseline = MapSource(simulation, source);
        var candidateStates = await repository.ListCandidatesAsync(id, cancellationToken);
        var candidates = candidateStates
            .Select(candidate => MapCandidate(candidate, simulation, source))
            .Where(candidate => candidate is not null)
            .Cast<ReplayCandidateDto>()
            .OrderByDescending(candidate => candidate.CreatedAt)
            .ToList();
        var summary = MapSummary(experiment, simulation.Title, candidates);
        return new ReplayExperimentDetailDto(summary, baseline, candidates);
    }

    public async Task<ReplayExperimentDetailDto> CreateExperimentAsync(
        CreateReplayExperimentCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateName(command.Name, "Experiment name");
        ValidateName(command.CandidateLabel, "Candidate label");
        var simulation = await simulations.GetAsync(command.SimulationId, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.simulation.not_found", $"Simulation '{command.SimulationId}' was not found.");
        var source = simulation.FindRun(command.SourceRunId)
            ?? throw new ResourceNotFoundException("replay.source.not_found", $"Source run '{command.SourceRunId}' was not found.");

        var replay = await ExecuteReplayAsync(command.SimulationId, source, command.CandidateLabel,
            command.Workflow, command.PromptVersion, command.KnowledgeCollection, command.Provider, command.Model,
            command.Temperature, command.MaxOutputTokens, command.Mode, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var experiment = new ReplayExperimentState(Guid.NewGuid(), command.Name.Trim(), command.SimulationId, command.SourceRunId, "Active", now, now);
        await repository.AddExperimentAsync(experiment, cancellationToken);
        await repository.AddCandidateAsync(MapCandidateState(experiment.Id, command.CandidateLabel, replay), cancellationToken);
        return await GetExperimentAsync(experiment.Id, cancellationToken);
    }

    public async Task<ReplayExperimentDetailDto> AddCandidateAsync(
        Guid experimentId,
        AddReplayCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateName(command.Label, "Candidate label");
        var experiment = await repository.GetExperimentAsync(experimentId, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.experiment.not_found", $"Replay experiment '{experimentId}' was not found.");
        if (!experiment.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only active replay experiments can accept new candidates.");
        var simulation = await simulations.GetAsync(experiment.SimulationId, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.simulation.not_found", $"Simulation '{experiment.SimulationId}' was not found.");
        var source = simulation.FindRun(experiment.SourceRunId)
            ?? throw new ResourceNotFoundException("replay.source.not_found", $"Source run '{experiment.SourceRunId}' was not found.");

        var replay = await ExecuteReplayAsync(experiment.SimulationId, source, command.Label,
            command.Workflow, command.PromptVersion, command.KnowledgeCollection, command.Provider, command.Model,
            command.Temperature, command.MaxOutputTokens, command.Mode, cancellationToken);
        await repository.AddCandidateAsync(MapCandidateState(experiment.Id, command.Label, replay), cancellationToken);
        await repository.UpdateExperimentAsync(experiment with { UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken);
        return await GetExperimentAsync(experiment.Id, cancellationToken);
    }

    public async Task<ReplayExperimentDetailDto> CompleteAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        var experiment = await repository.GetExperimentAsync(experimentId, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.experiment.not_found", $"Replay experiment '{experimentId}' was not found.");
        if ((await repository.ListCandidatesAsync(experimentId, cancellationToken)).Count == 0)
            throw new InvalidOperationException("A replay experiment requires at least one candidate before completion.");
        await repository.UpdateExperimentAsync(experiment with { Status = "Completed", UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken);
        return await GetExperimentAsync(experimentId, cancellationToken);
    }

    public async Task<ReplayExperimentDetailDto> ArchiveAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        var experiment = await repository.GetExperimentAsync(experimentId, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.experiment.not_found", $"Replay experiment '{experimentId}' was not found.");
        if (experiment.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
            return await GetExperimentAsync(experimentId, cancellationToken);
        if (!experiment.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleViolationException("replay.experiment.archive_requires_completion", "Complete the replay experiment before archiving it.");

        await repository.UpdateExperimentAsync(experiment with { Status = "Archived", UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken);
        return await GetExperimentAsync(experimentId, cancellationToken);
    }

    private async Task<SimulationRun> ExecuteReplayAsync(
        Guid simulationId,
        SimulationRun source,
        string label,
        string? workflow,
        string? promptVersion,
        string? knowledgeCollection,
        string provider,
        string? model,
        double temperature,
        int maxOutputTokens,
        SimulationMode mode,
        CancellationToken cancellationToken)
    {
        if (temperature is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0 and 2.");
        if (maxOutputTokens is < 32 or > 8192) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens), "Maximum output tokens must be between 32 and 8192.");
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Provider is required.", nameof(provider));

        var result = await simulationService.ReplayAsync(simulationId, new ReplaySimulationCommand(
            source.Id,
            mode,
            provider.Trim(),
            string.IsNullOrWhiteSpace(model) ? null : model.Trim(),
            temperature,
            maxOutputTokens,
            workflow,
            promptVersion,
            knowledgeCollection), cancellationToken)
            ?? throw new ResourceNotFoundException("replay.simulation.not_found", $"Simulation '{simulationId}' was not found.");

        return result.Runs
            .Where(run => run.ReplayedFromRunId == source.Id)
            .OrderByDescending(run => run.CreatedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Replay candidate '{label}' did not produce a run.");
    }

    private async Task SynchronizeExistingReplaysAsync(CancellationToken cancellationToken)
    {
        var all = await simulations.ListAsync(cancellationToken);
        foreach (var simulation in all)
        {
            var snapshot = simulation.Snapshot();
            foreach (var replay in snapshot.Runs.Where(run => run.ReplayedFromRunId.HasValue))
            {
                if (await repository.GetCandidateByRunAsync(replay.Id, cancellationToken) is not null) continue;
                var sourceRunId = replay.ReplayedFromRunId!.Value;
                if (simulation.FindRun(sourceRunId) is null) continue;
                var experiment = await repository.GetBySourceRunAsync(snapshot.Id, sourceRunId, cancellationToken);
                if (experiment is null)
                {
                    var createdAt = replay.CreatedAt;
                    experiment = new ReplayExperimentState(
                        Guid.NewGuid(),
                        $"Imported replay — {snapshot.Title}",
                        snapshot.Id,
                        sourceRunId,
                        "Active",
                        createdAt,
                        createdAt);
                    await repository.AddExperimentAsync(experiment, cancellationToken);
                }
                await repository.AddCandidateAsync(MapCandidateState(experiment.Id, $"Imported candidate {replay.CreatedAt:HH:mm:ss}", replay), cancellationToken);
                await repository.UpdateExperimentAsync(experiment with { UpdatedAt = replay.CreatedAt }, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<ReplaySourceDto>> ListSourcesInternalAsync(CancellationToken cancellationToken)
    {
        var all = await simulations.ListAsync(cancellationToken);
        return all
            .SelectMany(simulation => simulation.Snapshot().Runs.Select(run => MapSource(simulation, run)))
            .OrderByDescending(item => item.CreatedAt)
            .Take(250)
            .ToList();
    }

    private async Task<IReadOnlyList<ReplayExperimentSummaryDto>> BuildExperimentSummariesAsync(CancellationToken cancellationToken)
    {
        var experimentStates = await repository.ListExperimentsAsync(cancellationToken: cancellationToken);
        var simulationStates = (await simulations.ListAsync(cancellationToken)).ToDictionary(item => item.Id);
        var summaries = new List<ReplayExperimentSummaryDto>();
        foreach (var experiment in experimentStates)
        {
            if (!simulationStates.TryGetValue(experiment.SimulationId, out var simulation)) continue;
            var source = simulation.FindRun(experiment.SourceRunId);
            if (source is null) continue;
            var candidates = (await repository.ListCandidatesAsync(experiment.Id, cancellationToken))
                .Select(candidate => MapCandidate(candidate, simulation, source))
                .Where(candidate => candidate is not null)
                .Cast<ReplayCandidateDto>()
                .ToList();
            summaries.Add(MapSummary(experiment, simulation.Title, candidates));
        }
        return summaries.OrderByDescending(item => item.UpdatedAt).ToList();
    }

    private static ReplaySourceDto MapSource(SimulationState simulation, SimulationRun run)
    {
        var userMessage = simulation.FindMessage(run.UserMessageId)?.Content ?? "Message unavailable";
        var response = run.AssistantMessageId.HasValue ? simulation.FindMessage(run.AssistantMessageId.Value)?.Content : null;
        return new ReplaySourceDto(simulation.Id, simulation.Title, run.Id, run.ReplayedFromRunId, run.Status,
            userMessage, response, MapRunSnapshot(run, response), run.CreatedAt);
    }

    private static ReplayCandidateDto? MapCandidate(ReplayCandidateState candidate, SimulationState simulation, SimulationRun source)
    {
        var run = simulation.FindRun(candidate.RunId);
        if (run is null) return null;
        var response = run.AssistantMessageId.HasValue ? simulation.FindMessage(run.AssistantMessageId.Value)?.Content : null;
        var sourceResponse = source.AssistantMessageId.HasValue ? simulation.FindMessage(source.AssistantMessageId.Value)?.Content : null;
        var baseline = MapRunSnapshot(source, sourceResponse);
        var snapshot = MapRunSnapshot(run, response);
        return new ReplayCandidateDto(candidate.Id, candidate.ExperimentId, candidate.RunId, candidate.Label, run.Status,
            new ReplayConfigurationDto(candidate.Workflow, candidate.PromptVersion, candidate.KnowledgeCollection,
                candidate.Provider, candidate.Model, candidate.Temperature, candidate.MaxOutputTokens, candidate.Mode),
            snapshot, Compare(baseline, snapshot), candidate.CreatedAt);
    }

    private static ReplayExperimentSummaryDto MapSummary(
        ReplayExperimentState experiment,
        string simulationTitle,
        IReadOnlyList<ReplayCandidateDto> candidates)
    {
        var best = candidates
            .OrderByDescending(item => item.Snapshot.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Comparison.QualityDelta)
            .ThenBy(item => item.Snapshot.ActualCost)
            .FirstOrDefault();
        return new ReplayExperimentSummaryDto(
            experiment.Id,
            experiment.Name,
            experiment.SimulationId,
            simulationTitle,
            experiment.SourceRunId,
            experiment.Status,
            candidates.Count,
            best?.Id,
            best?.Comparison.QualityDelta ?? 0,
            experiment.CreatedAt,
            experiment.UpdatedAt);
    }

    private static ReplayCandidateState MapCandidateState(Guid experimentId, string label, SimulationRun run)
    {
        var configuration = ResolveConfiguration(run);
        return new ReplayCandidateState(
            Guid.NewGuid(), experimentId, run.Id, label.Trim(), configuration.Workflow, configuration.PromptVersion,
            configuration.KnowledgeCollection, configuration.Provider, configuration.Model, configuration.Temperature,
            configuration.MaxOutputTokens, configuration.Mode, run.CreatedAt);
    }

    private static ReplayRunSnapshotDto MapRunSnapshot(SimulationRun run, string? response)
    {
        var configuration = ResolveConfiguration(run);
        return new ReplayRunSnapshotDto(
            run.Id,
            run.Status,
            configuration.Workflow,
            run.Workflow?.Version ?? "Unknown",
            configuration.PromptVersion,
            configuration.KnowledgeCollection,
            run.ExecutionPlan?.Provider ?? configuration.Provider,
            run.ExecutionPlan?.Model ?? configuration.Model,
            configuration.Mode,
            configuration.Temperature,
            configuration.MaxOutputTokens,
            Quality(run.Evaluation),
            run.Evaluation.Groundedness,
            run.Evaluation.Relevance,
            run.Evaluation.Safety,
            run.Evaluation.Verdict,
            run.Metrics?.TotalDurationMs ?? 0,
            run.Metrics?.ProviderLatencyMs ?? 0,
            run.Metrics?.TotalTokens ?? 0,
            run.Metrics?.ActualCost ?? 0,
            run.Metrics?.Currency ?? "ZAR",
            run.ExecutionPlan?.Attempts ?? 0,
            run.ExecutionPlan?.FallbacksUsed ?? 0,
            run.KnowledgePackage.Citations.Count,
            response?.Length ?? 0,
            response,
            run.FailureReason,
            run.CreatedAt);
    }

    private static ReplayConfigurationDto ResolveConfiguration(SimulationRun run)
    {
        if (run.Configuration is not null)
            return new ReplayConfigurationDto(run.Configuration.Workflow, run.Configuration.PromptVersion,
                run.Configuration.KnowledgeCollection, run.Configuration.Provider, run.Configuration.Model,
                run.Configuration.Temperature, run.Configuration.MaxOutputTokens, run.Configuration.Mode);
        return new ReplayConfigurationDto(
            run.Workflow is null ? "Unknown workflow" : $"{run.Workflow.Name} v{run.Workflow.Version}",
            ExtractPromptVersion(run.RenderedPrompt),
            run.KnowledgePackage.Collection,
            run.ExecutionPlan?.Provider ?? "Unknown provider",
            run.ExecutionPlan?.Model ?? "Unknown model",
            ExtractHeaderDouble(run.RenderedPrompt, "TEMPERATURE", .2),
            ExtractHeaderInt(run.RenderedPrompt, "MAX_OUTPUT_TOKENS", 400),
            run.Mode);
    }

    private static ReplayComparisonDto Compare(ReplayRunSnapshotDto baseline, ReplayRunSnapshotDto candidate)
    {
        var qualityDelta = candidate.QualityScore - baseline.QualityScore;
        var durationDelta = candidate.DurationMs - baseline.DurationMs;
        var costDelta = candidate.ActualCost - baseline.ActualCost;
        var changed = new List<string>();
        if (!baseline.Workflow.Equals(candidate.Workflow, StringComparison.OrdinalIgnoreCase)) changed.Add("Workflow");
        if (!baseline.PromptVersion.Equals(candidate.PromptVersion, StringComparison.OrdinalIgnoreCase)) changed.Add("Prompt");
        if (!baseline.KnowledgeCollection.Equals(candidate.KnowledgeCollection, StringComparison.OrdinalIgnoreCase)) changed.Add("Knowledge");
        if (!baseline.Provider.Equals(candidate.Provider, StringComparison.OrdinalIgnoreCase)) changed.Add("Provider");
        if (!baseline.Model.Equals(candidate.Model, StringComparison.OrdinalIgnoreCase)) changed.Add("Model");
        if (Math.Abs(baseline.Temperature - candidate.Temperature) > .001) changed.Add("Temperature");
        if (baseline.MaxOutputTokens != candidate.MaxOutputTokens) changed.Add("Output limit");
        if (baseline.Mode != candidate.Mode) changed.Add("Recovery mode");

        string outcome;
        if (!candidate.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) && baseline.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            outcome = "Regression";
        else if (qualityDelta >= .01)
            outcome = "Improved";
        else if (qualityDelta <= -.01)
            outcome = "Regression";
        else if ((durationDelta < -25 || costDelta < 0) && qualityDelta >= -.005)
            outcome = "Efficient";
        else
            outcome = "Equivalent";

        var findings = new List<string>();
        findings.Add(qualityDelta switch
        {
            >= .01 => $"Quality improved by {qualityDelta:P1}.",
            <= -.01 => $"Quality regressed by {Math.Abs(qualityDelta):P1}.",
            _ => "Quality remained within the equivalence band."
        });
        if (durationDelta < -1) findings.Add($"Latency improved by {Math.Abs(durationDelta):F0} ms.");
        else if (durationDelta > 1) findings.Add($"Latency increased by {durationDelta:F0} ms.");
        if (costDelta < 0) findings.Add($"Cost decreased by {Math.Abs(costDelta):0.######} {candidate.Currency}.");
        else if (costDelta > 0) findings.Add($"Cost increased by {costDelta:0.######} {candidate.Currency}.");
        if (!baseline.Verdict.Equals(candidate.Verdict, StringComparison.OrdinalIgnoreCase))
            findings.Add($"Evaluation verdict changed from {baseline.Verdict} to {candidate.Verdict}.");
        if (changed.Count == 0) findings.Add("The candidate reused the baseline configuration.");
        else findings.Add($"Changed dimensions: {string.Join(", ", changed)}.");

        return new ReplayComparisonDto(
            qualityDelta,
            candidate.Groundedness - baseline.Groundedness,
            candidate.Relevance - baseline.Relevance,
            candidate.Safety - baseline.Safety,
            durationDelta,
            candidate.ProviderLatencyMs - baseline.ProviderLatencyMs,
            candidate.TotalTokens - baseline.TotalTokens,
            costDelta,
            candidate.CitationCount - baseline.CitationCount,
            candidate.ResponseLength - baseline.ResponseLength,
            outcome,
            changed,
            findings);
    }

    private static double Quality(SimulationEvaluation evaluation)
        => Math.Round((evaluation.Groundedness + evaluation.Relevance + evaluation.Safety) / 3d, 4);

    private static string ExtractPromptVersion(string renderedPrompt)
    {
        var line = renderedPrompt.Split('\n').FirstOrDefault(item => item.TrimStart().StartsWith("PROMPT VERSION:", StringComparison.OrdinalIgnoreCase));
        return line is null ? "Captured prompt" : line[(line.IndexOf(':') + 1)..].Trim();
    }

    private static double ExtractHeaderDouble(string prompt, string key, double fallback)
    {
        var value = ExtractHeader(prompt, key);
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static int ExtractHeaderInt(string prompt, string key, int fallback)
        => int.TryParse(ExtractHeader(prompt, key), out var parsed) ? parsed : fallback;

    private static string? ExtractHeader(string prompt, string key)
    {
        var prefix = $"[{key}:";
        var line = prompt.Split('\n').FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line is null ? null : line[prefix.Length..].TrimEnd(']').Trim();
    }

    private static void ValidateName(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{field} is required.");
        if (value.Trim().Length > 200) throw new ArgumentException($"{field} cannot exceed 200 characters.");
    }
}
