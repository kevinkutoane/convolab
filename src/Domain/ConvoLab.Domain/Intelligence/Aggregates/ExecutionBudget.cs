using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Aggregates;

/// <summary>
/// A spending envelope for intelligent execution, scoped to a tenant,
/// conversation, workflow, or the platform. Budgets are enforced at planning
/// time (reservations against estimates) and reconciled at completion
/// (actuals). An exhausted budget halts planning — money is a domain invariant
/// here, not a dashboard afterthought.
/// </summary>
public class ExecutionBudget : BaseAggregateRoot<ExecutionBudgetId>
{
    public string Name { get; private set; }
    public CostAttribution Scope { get; private set; }
    public ExecutionCost Limit { get; private set; }
    public ExecutionCost Consumed { get; private set; }
    public ExecutionCost Reserved { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }

    public ExecutionCost Remaining => ExecutionCost.Create(
        Math.Max(0m, Limit.Amount - Consumed.Amount - Reserved.Amount), Limit.Currency);

    public bool IsExhausted => Consumed.Amount + Reserved.Amount >= Limit.Amount;

    private ExecutionBudget() : base()
    {
        Name = null!;
        Scope = null!;
        Limit = null!;
        Consumed = null!;
        Reserved = null!;
    } // For EF Core

    private ExecutionBudget(ExecutionBudgetId id, string name, CostAttribution scope, ExecutionCost limit, DateTime periodStart, DateTime periodEnd) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Budget name is required.");
        if (limit.Amount <= 0) throw new ArgumentException("Budget limit must be positive.");
        if (periodEnd <= periodStart) throw new ArgumentException("Budget period end must be after start.");

        Name = name;
        Scope = scope;
        Limit = limit;
        Consumed = ExecutionCost.Zero(limit.Currency);
        Reserved = ExecutionCost.Zero(limit.Currency);
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    public static ExecutionBudget Create(string name, ExecutionCost limit, CostAttribution? scope = null, DateTime? periodStart = null, DateTime? periodEnd = null)
    {
        var start = periodStart ?? DateTime.UtcNow;
        return new ExecutionBudget(
            ExecutionBudgetId.CreateUnique(), name, scope ?? CostAttribution.None(), limit,
            start, periodEnd ?? start.AddMonths(1));
    }

    /// <summary>
    /// Reserves the estimated cost for a planned execution. Throws when the
    /// reservation would breach the limit — the planner must not produce plans
    /// the platform cannot afford.
    /// </summary>
    public void Reserve(ExecutionCost estimatedCost)
    {
        if (Consumed.Amount + Reserved.Amount + estimatedCost.Amount > Limit.Amount)
        {
            AddDomainEvent(new BudgetExhaustedEvent(Id, Limit));
            throw new InvalidOperationException(
                $"Budget '{Name}' cannot reserve {estimatedCost.Amount} {estimatedCost.Currency}: " +
                $"remaining {Remaining.Amount} {Remaining.Currency}.");
        }

        Reserved = Reserved.Add(estimatedCost);
    }

    /// <summary>
    /// Settles a reservation with the actual cost: releases the reservation
    /// and consumes the actual amount.
    /// </summary>
    public void Settle(ExecutionCost reservedCost, ExecutionCost actualCost)
    {
        var newReserved = Math.Max(0m, Reserved.Amount - reservedCost.Amount);
        Reserved = ExecutionCost.Create(newReserved, Limit.Currency);
        Consumed = Consumed.Add(actualCost);

        AddDomainEvent(new UsagePublishedEvent(Id, Consumed, Remaining));

        if (IsExhausted)
            AddDomainEvent(new BudgetExhaustedEvent(Id, Limit));
    }

    /// <summary>Releases a reservation without consumption (cancelled/failed executions).</summary>
    public void Release(ExecutionCost reservedCost)
    {
        var newReserved = Math.Max(0m, Reserved.Amount - reservedCost.Amount);
        Reserved = ExecutionCost.Create(newReserved, Limit.Currency);
    }
}
