using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.IntelligenceStudio;

public sealed class IntelligenceStudioService : IIntelligenceStudioService
{
    private const string ReportingCurrency = "ZAR";
    private readonly IConversationSimulationStore _simulations;
    private readonly IIntelligenceStudioConfiguration _configuration;

    public IntelligenceStudioService(
        IConversationSimulationStore simulations,
        IIntelligenceStudioConfiguration configuration)
    {
        _simulations = simulations;
        _configuration = configuration;
    }

    public async Task<IntelligenceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var executions = await LoadExecutionsAsync(cancellationToken);
        var providers = _configuration.GetProviders();
        var completed = executions.Where(item => item.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)).ToList();
        var reportingExecutions = executions
            .Where(item => item.Currency.Equals(ReportingCurrency, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var totalCost = reportingExecutions.Sum(item => item.Cost);
        var totalTokens = executions.Sum(item => (long)item.TotalTokens);
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);
        var periodExecutions = reportingExecutions.Where(item => item.CreatedAt >= periodStart && item.CreatedAt < periodEnd).ToList();
        var consumed = periodExecutions.Sum(item => item.Cost);
        var limit = Math.Max(0m, _configuration.MonthlyBudgetZar);
        var remaining = Math.Max(0m, limit - consumed);
        var utilisation = limit == 0m ? 0d : Math.Min(1d, (double)(consumed / limit));
        var budgetStatus = limit == 0m ? "Not configured" : utilisation >= 1 ? "Exhausted" : utilisation >= .8 ? "Warning" : "Healthy";

        var metrics = new IntelligenceMetricsDto(
            executions.Count,
            completed.Count,
            executions.Count == 0 ? 0d : completed.Count / (double)executions.Count,
            executions.Count == 0 ? 0d : executions.Average(item => item.DurationMs),
            totalTokens,
            totalCost,
            ReportingCurrency,
            executions.Count(item => item.Attempts > 1),
            executions.Count(item => item.FallbacksUsed > 0));

        var providerUsage = executions
            .Where(item => !string.IsNullOrWhiteSpace(item.Provider))
            .GroupBy(item => item.Provider, StringComparer.OrdinalIgnoreCase)
            .Select(group => new IntelligenceProviderUsageDto(
                group.Key,
                group.Count(),
                group.Count(item => item.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) / (double)group.Count(),
                group.Average(item => item.DurationMs),
                group.Sum(item => (long)item.TotalTokens),
                group.Where(item => item.Currency.Equals(ReportingCurrency, StringComparison.OrdinalIgnoreCase)).Sum(item => item.Cost),
                ReportingCurrency))
            .OrderByDescending(item => item.Executions)
            .ToList();

        var dailyUsage = Enumerable.Range(0, 7)
            .Select(offset => DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(offset - 6)))
            .Select(date =>
            {
                var items = executions.Where(item => DateOnly.FromDateTime(item.CreatedAt.UtcDateTime) == date).ToList();
                return new IntelligenceDailyUsageDto(
                    date,
                    items.Count,
                    items.Sum(item => (long)item.TotalTokens),
                    items.Where(item => item.Currency.Equals(ReportingCurrency, StringComparison.OrdinalIgnoreCase)).Sum(item => item.Cost),
                    items.Count == 0 ? 0d : items.Average(item => item.DurationMs));
            })
            .ToList();

        return new IntelligenceOverviewDto(
            metrics,
            new IntelligenceBudgetDto(limit, consumed, remaining, utilisation, ReportingCurrency, periodStart, periodEnd, budgetStatus),
            providers,
            providerUsage,
            dailyUsage,
            executions.Take(25).ToList(),
            now);
    }

    public async Task<IReadOnlyList<IntelligenceExecutionDto>> ListExecutionsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var executions = await LoadExecutionsAsync(cancellationToken);
        return executions.Take(Math.Clamp(limit, 1, 500)).ToList();
    }

    public async Task<ExecutionPlanPreviewDto> PreviewPlanAsync(
        ExecutionPlanPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidatePreview(command);

        var providers = _configuration.GetProviders();
        var provider = providers.FirstOrDefault(item => item.Key.Equals(command.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new ResourceNotFoundException("intelligence.provider_not_found", $"Provider '{command.Provider}' was not found.");
        var model = provider.Models.FirstOrDefault(item => item.Key.Equals(command.Model, StringComparison.OrdinalIgnoreCase))
            ?? throw new ResourceNotFoundException("intelligence.model_not_found", $"Model '{command.Model}' was not found for provider '{provider.DisplayName}'.");

        var inputTokens = command.EstimatedInputTokens;
        var outputTokens = command.MaxOutputTokens;
        var totalTokens = (long)inputTokens + outputTokens;
        var contextWithinLimit = totalTokens <= model.MaxContextTokens;
        var outputWithinLimit = outputTokens <= model.MaxOutputTokens;
        var required = (command.RequiredCapabilities ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var capabilityMatch = required.All(value => model.Capabilities.Contains(value, StringComparer.OrdinalIgnoreCase));
        decimal? estimatedCost = null;
        if (model.InputPricePer1K is not null && model.OutputPricePer1K is not null)
        {
            estimatedCost = Math.Round(
                inputTokens / 1000m * model.InputPricePer1K.Value
                + outputTokens / 1000m * model.OutputPricePer1K.Value,
                6);
        }

        var overview = await GetOverviewAsync(cancellationToken);
        var withinBudget = estimatedCost is null || estimatedCost.Value <= overview.Budget.Remaining;
        var decisions = new List<ExecutionPlanDecisionDto>
        {
            new("Provider readiness", provider.IsConfigured ? "Approved" : "Blocked", provider.IsConfigured ? $"{provider.DisplayName} is configured." : provider.ConfigurationHint ?? "Provider configuration is incomplete."),
            new("Capability match", capabilityMatch ? "Approved" : "Blocked", capabilityMatch ? "The model satisfies all required capabilities." : "The model does not satisfy every requested capability."),
            new("Context window", contextWithinLimit ? "Approved" : "Blocked", $"Requested {totalTokens:N0} of {model.MaxContextTokens:N0} context tokens."),
            new("Output limit", outputWithinLimit ? "Approved" : "Blocked", $"Requested {outputTokens:N0} of {model.MaxOutputTokens:N0} output tokens."),
            new("Budget admission", withinBudget ? "Approved" : "Blocked", estimatedCost is null ? "Pricing is not configured for this model." : $"Estimated {estimatedCost:0.000000} {model.Currency}; {overview.Budget.Remaining:0.000000} ZAR remains this month."),
            new("Recovery policy", command.AllowFallback ? "Enabled" : "Disabled", $"Maximum {command.MaxAttempts} attempt(s); fallback {(command.AllowFallback ? "allowed" : "not allowed")}.")
        };

        var warnings = new List<string>();
        if (!provider.IsConfigured) warnings.Add(provider.ConfigurationHint ?? "Provider is not configured.");
        if (!capabilityMatch) warnings.Add("Select a model that supports all requested capabilities.");
        if (!contextWithinLimit) warnings.Add("The requested input and output exceed the model context window.");
        if (!outputWithinLimit) warnings.Add("The requested output exceeds the model output limit.");
        if (!withinBudget) warnings.Add("The projected execution exceeds the remaining monthly budget.");
        if (estimatedCost is null) warnings.Add("Model pricing is not configured, so cost admission is informational only.");
        if (command.Streaming && !model.Capabilities.Contains("Streaming", StringComparer.OrdinalIgnoreCase))
            warnings.Add("Streaming was requested but is not declared by the selected model.");

        return new ExecutionPlanPreviewDto(
            provider.Key,
            model.Key,
            provider.IsConfigured,
            capabilityMatch,
            inputTokens,
            outputTokens,
            totalTokens,
            estimatedCost,
            model.Currency,
            model.TypicalLatencyMs,
            overview.Budget.Remaining,
            withinBudget,
            decisions,
            warnings);
    }

    private async Task<List<IntelligenceExecutionDto>> LoadExecutionsAsync(CancellationToken cancellationToken)
    {
        var simulations = await _simulations.ListAsync(cancellationToken);
        return simulations
            .Select(state => state.Snapshot())
            .SelectMany(simulation => simulation.Runs.Select(run => MapExecution(simulation, run)))
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
    }

    private static void ValidatePreview(ExecutionPlanPreviewCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Provider))
            errors["provider"] = ["A provider is required."];
        if (string.IsNullOrWhiteSpace(command.Model))
            errors["model"] = ["A model is required."];
        if (command.EstimatedInputTokens <= 0)
            errors["estimatedInputTokens"] = ["Estimated input tokens must be greater than zero."];
        if (command.MaxOutputTokens <= 0)
            errors["maxOutputTokens"] = ["Maximum output tokens must be greater than zero."];
        if (command.MaxAttempts is < 1 or > 10)
            errors["maxAttempts"] = ["Maximum attempts must be between 1 and 10."];

        if (errors.Count > 0)
            throw new RequestValidationException(
                "intelligence.plan.invalid",
                "The execution plan preview request is invalid.",
                errors);
    }

    private static IntelligenceExecutionDto MapExecution(SimulationConversation simulation, SimulationRun run)
    {
        var plan = run.ExecutionPlan;
        var metrics = run.Metrics;
        return new IntelligenceExecutionDto(
            simulation.Id,
            simulation.Title,
            run.Id,
            run.Status,
            run.Mode.ToString(),
            plan?.Provider ?? "Not planned",
            plan?.Model ?? "Not planned",
            plan?.Attempts ?? 0,
            plan?.FallbacksUsed ?? 0,
            metrics?.InputTokens ?? 0,
            metrics?.OutputTokens ?? 0,
            metrics?.TotalTokens ?? 0,
            metrics?.ActualCost ?? 0m,
            metrics?.Currency ?? plan?.Currency ?? ReportingCurrency,
            metrics?.TotalDurationMs ?? 0d,
            metrics?.ProviderLatencyMs ?? 0d,
            run.Evaluation.Groundedness,
            run.Evaluation.Relevance,
            run.Evaluation.Verdict,
            run.CreatedAt,
            run.FailureReason);
    }
}
