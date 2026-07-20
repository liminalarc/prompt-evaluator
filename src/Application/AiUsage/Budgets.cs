using Domain;

namespace Application.AiUsage;

/// <summary>Persistence port for AI-usage budgets (6.1.T6).</summary>
public interface IAiUsageBudgetRepository
{
    Task AddAsync(AiUsageBudget budget, CancellationToken ct = default);
    Task<IReadOnlyList<AiUsageBudget>> ListAsync(CancellationToken ct = default);
    Task<AiUsageBudget?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>A budget plus its current-period spend and where that sits against the limit (6.1.T6).</summary>
public sealed record BudgetSpendStatus(
    AiUsageBudget Budget, decimal SpendUsd, double PercentUsed, BudgetStatusLevel Level);

/// <summary>
/// Computes spend-vs-budget status for every configured budget (6.1.T6): for each budget it derives a
/// ledger filter from its scope over the current period and reads the spend from the usage read model,
/// then classifies OK / Warning / Over. Tracking + alerting only — no run-path enforcement.
/// </summary>
public sealed class BudgetStatusHandler(
    IAiUsageBudgetRepository budgets, IAiUsageQueries queries, TimeProvider time)
{
    public async Task<IReadOnlyList<BudgetSpendStatus>> StatusAsync(CancellationToken ct = default)
    {
        var all = await budgets.ListAsync(ct);
        var now = time.GetUtcNow();
        var periodStart = new DateTimeOffset(new DateTime(now.Year, now.Month, 1), TimeSpan.Zero);

        var result = new List<BudgetSpendStatus>(all.Count);
        foreach (var budget in all)
        {
            var filter = ToFilter(budget, periodStart);
            var spend = (await queries.SummaryAsync(filter, ct)).TotalCostUsd;
            var percent = budget.LimitUsd == 0 ? 0 : (double)(spend / budget.LimitUsd) * 100.0;
            result.Add(new BudgetSpendStatus(budget, spend, percent, budget.Classify(spend)));
        }
        return result;
    }

    private static AiUsageFilter ToFilter(AiUsageBudget budget, DateTimeOffset periodStart) => budget.Scope switch
    {
        BudgetScope.Model => new AiUsageFilter(From: periodStart, Models: [budget.ScopeValue!]),
        BudgetScope.Feature when Enum.TryParse<AiUsageFeature>(budget.ScopeValue, ignoreCase: true, out var f)
            => new AiUsageFilter(From: periodStart, Features: [f]),
        BudgetScope.Organization when Guid.TryParse(budget.ScopeValue, out var org)
            => new AiUsageFilter(From: periodStart, Organizations: [org]),
        _ => new AiUsageFilter(From: periodStart),
    };
}
