using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.Services;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Application.Services;

/// <summary>
/// Capability 6 — Intelligence Engine application service.
///
/// Coordinates the full execution lifecycle across pure domain concepts:
/// planning (ExecutionPlanner), safety validation, budget reservation,
/// execution via the provider-independent IIntelligenceExecutor port, retry
/// and fallback disposition, evaluation, telemetry recording, and budget
/// settlement. Contains no provider knowledge whatsoever.
/// </summary>
public class IntelligenceEngine : IIntelligenceEngine
{
    private readonly IIntelligenceProviderRepository _providers;
    private readonly IExecutionRequestRepository _executions;
    private readonly IExecutionBudgetRepository _budgets;
    private readonly IIntelligenceExecutor _executor;
    private readonly ExecutionPlanner _planner;

    public IntelligenceEngine(
        IIntelligenceProviderRepository providers,
        IExecutionRequestRepository executions,
        IExecutionBudgetRepository budgets,
        IIntelligenceExecutor executor,
        ExecutionPlanner planner)
    {
        _providers = providers;
        _executions = executions;
        _budgets = budgets;
        _executor = executor;
        _planner = planner;
    }

    // ── Provider & model catalogue ──────────────────────────────────────

    public async Task<IntelligenceProviderId> RegisterProviderAsync(
        string name, ProviderKind kind, RateLimitWindow? rateLimits = null,
        CancellationToken cancellationToken = default)
    {
        var provider = IntelligenceProvider.Register(name, kind, rateLimits);
        await _providers.AddAsync(provider, cancellationToken);
        return provider.Id;
    }

    public async Task<IntelligenceModelId> RegisterModelAsync(
        IntelligenceProviderId providerId, string name, CapabilitySet capabilities,
        ModelPricing pricing, int maxContextTokens, int maxOutputTokens,
        TimeSpan? typicalLatency = null, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetByIdAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException($"Provider '{providerId}' is not registered.");

        var model = provider.AddModel(name, capabilities, pricing, maxContextTokens, maxOutputTokens, typicalLatency);
        await _providers.UpdateAsync(provider, cancellationToken);
        return model.Id;
    }

    public async Task ReportProviderHealthAsync(
        IntelligenceProviderId providerId, ProviderHealthSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetByIdAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException($"Provider '{providerId}' is not registered.");

        provider.ReportHealth(snapshot);
        await _providers.UpdateAsync(provider, cancellationToken);
    }

    // ── Planning ────────────────────────────────────────────────────────

    public async Task<ExecutionPlan> PlanExecutionAsync(
        Domain.Intelligence.ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        ExecutionPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        var providers = await _providers.GetAllAsync(cancellationToken);
        return _planner.CreatePlan(context, requirement, policy ?? ExecutionPolicy.Default(), providers);
    }

    // ── Execution ───────────────────────────────────────────────────────

    public async Task<ExecutionResponse> ExecuteAsync(
        Domain.Intelligence.ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        string renderedPrompt,
        ExecutionPolicy? policy = null,
        ExecutionBudgetId? budgetId = null,
        CancellationToken cancellationToken = default)
    {
        var effectivePolicy = policy ?? ExecutionPolicy.Default();
        var request = ExecutionRequest.Create(context, requirement);
        await _executions.AddAsync(request, cancellationToken);

        var startedAt = DateTime.UtcNow;
        ExecutionBudget? budget = null;
        ExecutionCost reserved = ExecutionCost.Zero();

        try
        {
            // 1. Plan — provider/model selection, estimates, policy validation.
            var providers = await _providers.GetAllAsync(cancellationToken);
            var plan = _planner.CreatePlan(context, requirement, effectivePolicy, providers);
            request.AttachPlan(plan);

            // 2. Budget reservation at planning time, against the estimate.
            if (budgetId is not null)
            {
                budget = await _budgets.GetByIdAsync(budgetId, cancellationToken)
                    ?? throw new InvalidOperationException($"Budget '{budgetId}' does not exist.");
                budget.Reserve(plan.EstimatedCost);
                reserved = plan.EstimatedCost;
                await _budgets.UpdateAsync(budget, cancellationToken);
            }

            // 3. Safety validation. Placeholder pipeline approves by default;
            //    dedicated evaluators are attached via the safety stages later.
            request.Validate(SafetyDecision.ApprovedWithoutChecks());
            if (request.Status == ExecutionStatus.Failed)
                return await FailAsync(request, budget, reserved, "Safety pipeline rejected the execution.", cancellationToken);

            // 4. Execute with retry and fallback governed by the plan.
            var result = await ExecuteWithRecoveryAsync(request, context, requirement, effectivePolicy, renderedPrompt, cancellationToken);
            if (result is null)
            {
                var reason = request.Failures.LastOrDefault()?.Reason ?? "Execution failed.";
                return await FailAsync(request, budget, reserved, reason, cancellationToken);
            }

            // 5. Complete → evaluate → record → finish.
            request.Complete(result);
            request.MarkEvaluated();

            var telemetry = ExecutionTelemetry.Create(
                DateTime.UtcNow - startedAt,
                request.Plan!.EstimatedLatency,
                Math.Max(1, request.AttemptNumber),
                request.FallbacksUsed,
                result.Usage,
                result.ActualCost);

            request.Record(telemetry);
            request.Finish();
            await _executions.UpdateAsync(request, cancellationToken);

            // 6. Budget settlement with actuals.
            if (budget is not null)
            {
                budget.Settle(reserved, result.ActualCost);
                await _budgets.UpdateAsync(budget, cancellationToken);
            }

            return ExecutionResponse.Success(request.Id, request.Status, result, telemetry);
        }
        catch (InvalidOperationException ex)
        {
            return await FailAsync(request, budget, reserved, ex.Message, cancellationToken);
        }
    }

    public async Task CancelExecutionAsync(
        ExecutionRequestId requestId, string reason,
        CancellationToken cancellationToken = default)
    {
        var request = await _executions.GetByIdAsync(requestId, cancellationToken)
            ?? throw new InvalidOperationException($"Execution '{requestId}' does not exist.");

        request.Cancel(reason);
        await _executions.UpdateAsync(request, cancellationToken);
    }

    public Task<ExecutionRequest?> GetExecutionAsync(
        ExecutionRequestId requestId, CancellationToken cancellationToken = default)
        => _executions.GetByIdAsync(requestId, cancellationToken);

    // ── Budgets & usage ─────────────────────────────────────────────────

    public async Task<ExecutionBudgetId> CreateBudgetAsync(
        string name, ExecutionCost limit, CostAttribution? scope = null,
        CancellationToken cancellationToken = default)
    {
        var budget = ExecutionBudget.Create(name, limit, scope);
        await _budgets.AddAsync(budget, cancellationToken);
        return budget.Id;
    }

    public async Task<ExecutionCost> GetRemainingBudgetAsync(
        ExecutionBudgetId budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgets.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new InvalidOperationException($"Budget '{budgetId}' does not exist.");
        return budget.Remaining;
    }

    // ── Internals ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs the execution loop honouring the plan's retry policy and fallback
    /// chain. Returns null when every avenue is exhausted.
    /// </summary>
    private async Task<ExecutionResult?> ExecuteWithRecoveryAsync(
        ExecutionRequest request,
        Domain.Intelligence.ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        ExecutionPolicy policy,
        string renderedPrompt,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            request.Begin();

            try
            {
                var result = await _executor.ExecuteAsync(request, renderedPrompt, cancellationToken);
                await RecordProviderOutcomeAsync(request.Plan!.ProviderId, success: true, cancellationToken);
                return result;
            }
            catch (OperationCanceledException)
            {
                request.Cancel("Cancelled by caller.");
                await _executions.UpdateAsync(request, cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                await RecordProviderOutcomeAsync(request.Plan!.ProviderId, success: false, cancellationToken);

                var kind = ClassifyFailure(ex);
                var disposition = request.RegisterFailure(kind, ex.Message);

                switch (disposition)
                {
                    case FailureDisposition.Retry:
                        continue; // Begin() again under the same plan.

                    case FailureDisposition.Fallback:
                        var nextModel = request.Plan.FallbackPolicy.NextAfter(request.FallbacksUsed);
                        if (nextModel is null) return null;

                        var providers = await _providers.GetAllAsync(cancellationToken);
                        try
                        {
                            var fallbackPlan = _planner.CreateFallbackPlan(context, requirement, policy, providers, nextModel);
                            request.ApplyFallbackPlan(fallbackPlan);
                            continue;
                        }
                        catch (InvalidOperationException)
                        {
                            return null; // Fallback model no longer routable.
                        }

                    default:
                        return null; // Terminal.
                }
            }
        }
    }

    /// <summary>Feeds success/failure signals into the provider's circuit breaker.</summary>
    private async Task RecordProviderOutcomeAsync(IntelligenceProviderId providerId, bool success, CancellationToken cancellationToken)
    {
        var provider = await _providers.GetByIdAsync(providerId, cancellationToken);
        if (provider is null) return;

        if (success) provider.RecordSuccess();
        else provider.RecordFailure();

        await _providers.UpdateAsync(provider, cancellationToken);
    }

    /// <summary>Maps infrastructure exceptions to domain failure kinds.</summary>
    private static FailureKind ClassifyFailure(Exception ex) => ex switch
    {
        TimeoutException => FailureKind.Timeout,
        HttpRequestException => FailureKind.Transient,
        InvalidOperationException => FailureKind.Permanent,
        _ => FailureKind.Transient
    };

    private async Task<ExecutionResponse> FailAsync(
        ExecutionRequest request, ExecutionBudget? budget, ExecutionCost reserved,
        string reason, CancellationToken cancellationToken)
    {
        await _executions.UpdateAsync(request, cancellationToken);

        if (budget is not null && reserved.Amount > 0)
        {
            budget.Release(reserved);
            await _budgets.UpdateAsync(budget, cancellationToken);
        }

        return ExecutionResponse.Failure(request.Id, request.Status, reason);
    }
}
