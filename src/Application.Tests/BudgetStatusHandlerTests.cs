using Application.AiUsage;
using Domain;

namespace Application.Tests;

public class BudgetStatusHandlerTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeQueries(decimal spend) : IAiUsageQueries
    {
        public AiUsageFilter? LastFilter { get; private set; }

        public Task<AiUsageMetrics> SummaryAsync(AiUsageFilter filter, CancellationToken ct = default)
        {
            LastFilter = filter;
            return Task.FromResult(AiUsageMetrics.Empty with { TotalCostUsd = spend });
        }

        public Task<IReadOnlyList<AiUsageBreakdownRow>> BreakdownAsync(AiUsageFilter f, AiUsageDimension d, int? topN = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<AiUsageTimePoint>> TimeSeriesAsync(AiUsageFilter f, AiUsagePeriod p, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AiUsageCallsPage> CallsAsync(AiUsageFilter f, int page, int pageSize, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<AiUsageCall>> ExportAsync(AiUsageFilter f, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeBudgetRepo(params AiUsageBudget[] budgets) : IAiUsageBudgetRepository
    {
        private readonly List<AiUsageBudget> _items = budgets.ToList();
        public Task AddAsync(AiUsageBudget b, CancellationToken ct = default) { _items.Add(b); return Task.CompletedTask; }
        public Task<IReadOnlyList<AiUsageBudget>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AiUsageBudget>>(_items);
        public Task<AiUsageBudget?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(b => b.Id == id));
        public Task<bool> RemoveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_items.RemoveAll(b => b.Id == id) > 0);
    }

    [Theory]
    [InlineData(50, BudgetStatusLevel.Ok)]
    [InlineData(80, BudgetStatusLevel.Warning)]
    [InlineData(100, BudgetStatusLevel.Over)]
    [InlineData(120, BudgetStatusLevel.Over)]
    public async Task Status_classifies_spend_against_a_global_budget(int spend, BudgetStatusLevel expected)
    {
        var budget = AiUsageBudget.Create(BudgetScope.Global, null, 100m, BudgetPeriod.Monthly, 80, When);
        var handler = new BudgetStatusHandler(new FakeBudgetRepo(budget), new FakeQueries(spend), new FixedTime(When));

        var status = Assert.Single(await handler.StatusAsync());

        Assert.Equal(expected, status.Level);
        Assert.Equal(spend, status.SpendUsd);
        Assert.Equal((double)spend, status.PercentUsed, 3); // limit 100 → percent == spend
    }

    [Fact]
    public async Task Status_scopes_a_model_budget_to_the_current_month_and_that_model()
    {
        var budget = AiUsageBudget.Create(BudgetScope.Model, "claude-opus-4-8", 50m, BudgetPeriod.Monthly, 80, When);
        var queries = new FakeQueries(10m);
        var handler = new BudgetStatusHandler(new FakeBudgetRepo(budget), queries, new FixedTime(When));

        await handler.StatusAsync();

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), queries.LastFilter!.From);
        Assert.Equal(new[] { "claude-opus-4-8" }, queries.LastFilter.Models);
    }

    [Fact]
    public async Task Status_scopes_a_feature_budget_to_that_feature()
    {
        var budget = AiUsageBudget.Create(BudgetScope.Feature, "LlmJudge", 50m, BudgetPeriod.Monthly, 80, When);
        var queries = new FakeQueries(5m);
        var handler = new BudgetStatusHandler(new FakeBudgetRepo(budget), queries, new FixedTime(When));

        await handler.StatusAsync();

        Assert.Equal(new[] { AiUsageFeature.LlmJudge }, queries.LastFilter!.Features);
    }
}
