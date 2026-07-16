using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Application.Common.Interfaces;

/// <summary>
/// Capability 6 — the Intelligence Engine.
///
/// The platform's single gateway to intelligent execution. Callers describe
/// WHAT they need (context, requirement, policy); the engine decides HOW —
/// provider and model selection, planning, safety validation, execution,
/// streaming, tool calling, retry, fallback, budget accounting, and telemetry.
///
/// No provider, SDK, or wire-format type ever crosses this boundary.
/// </summary>
public interface IIntelligenceEngine
{
    // ── Provider & model catalogue ──────────────────────────────────────

    /// <summary>Registers an intelligence provider in the catalogue.</summary>
    Task<IntelligenceProviderId> RegisterProviderAsync(
        string name, ProviderKind kind, RateLimitWindow? rateLimits = null,
        CancellationToken cancellationToken = default);

    /// <summary>Registers a model under a provider with capabilities, pricing, and limits.</summary>
    Task<IntelligenceModelId> RegisterModelAsync(
        IntelligenceProviderId providerId, string name, CapabilitySet capabilities,
        ModelPricing pricing, int maxContextTokens, int maxOutputTokens,
        TimeSpan? typicalLatency = null, CancellationToken cancellationToken = default);

    /// <summary>Reports a provider health observation (availability, latency, error rate, circuit).</summary>
    Task ReportProviderHealthAsync(
        IntelligenceProviderId providerId, ProviderHealthSnapshot snapshot,
        CancellationToken cancellationToken = default);

    // ── Planning ────────────────────────────────────────────────────────

    /// <summary>
    /// Plans an execution without running it: capability matching, provider
    /// and model selection, cost/latency estimation, and policy validation.
    /// Useful for previews and admission control.
    /// </summary>
    Task<ExecutionPlan> PlanExecutionAsync(
        Domain.Intelligence.ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        ExecutionPolicy? policy = null,
        CancellationToken cancellationToken = default);

    // ── Execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes an intelligent workload end-to-end: plan → validate →
    /// execute (with retry and fallback) → complete → evaluate → record.
    /// Returns the normalized response with telemetry.
    /// </summary>
    Task<ExecutionResponse> ExecuteAsync(
        Domain.Intelligence.ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        string renderedPrompt,
        ExecutionPolicy? policy = null,
        ExecutionBudgetId? budgetId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels an in-flight execution.</summary>
    Task CancelExecutionAsync(
        ExecutionRequestId requestId, string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves the current state of an execution.</summary>
    Task<ExecutionRequest?> GetExecutionAsync(
        ExecutionRequestId requestId, CancellationToken cancellationToken = default);

    // ── Budgets & usage ─────────────────────────────────────────────────

    /// <summary>Creates a spending envelope scoped to a tenant, conversation, workflow, or platform.</summary>
    Task<ExecutionBudgetId> CreateBudgetAsync(
        string name, ExecutionCost limit, CostAttribution? scope = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the remaining headroom of a budget.</summary>
    Task<ExecutionCost> GetRemainingBudgetAsync(
        ExecutionBudgetId budgetId, CancellationToken cancellationToken = default);
}
