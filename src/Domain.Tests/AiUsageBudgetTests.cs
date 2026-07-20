namespace Domain.Tests;

public class AiUsageBudgetTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_a_global_budget()
    {
        var b = AiUsageBudget.Create(BudgetScope.Global, null, 100m, BudgetPeriod.Monthly, 80, When);

        Assert.Equal(BudgetScope.Global, b.Scope);
        Assert.Null(b.ScopeValue);
        Assert.Equal(100m, b.LimitUsd);
        Assert.Equal(80, b.AlertThresholdPercent);
    }

    [Fact]
    public void Create_a_scoped_budget_requires_a_scope_value()
    {
        var scoped = AiUsageBudget.Create(BudgetScope.Model, "claude-opus-4-8", 50m, BudgetPeriod.Monthly, 90, When);
        Assert.Equal("claude-opus-4-8", scoped.ScopeValue);

        Assert.Throws<ArgumentException>(() =>
            AiUsageBudget.Create(BudgetScope.Model, "  ", 50m, BudgetPeriod.Monthly, 90, When));
    }

    [Fact]
    public void Create_a_global_budget_rejects_a_scope_value()
    {
        Assert.Throws<ArgumentException>(() =>
            AiUsageBudget.Create(BudgetScope.Global, "something", 50m, BudgetPeriod.Monthly, 80, When));
    }

    [Theory]
    [InlineData("SubjectExecution")]
    [InlineData("llmjudge")] // case-insensitive
    [InlineData("SyntheticGeneration")]
    public void Create_a_feature_budget_accepts_a_valid_feature(string feature)
    {
        var b = AiUsageBudget.Create(BudgetScope.Feature, feature, 50m, BudgetPeriod.Monthly, 80, When);
        Assert.Equal(BudgetScope.Feature, b.Scope);
    }

    [Fact]
    public void Create_a_feature_budget_rejects_an_unknown_feature()
    {
        // Without validation this would be created, then silently track the WHOLE ledger.
        Assert.Throws<ArgumentException>(() =>
            AiUsageBudget.Create(BudgetScope.Feature, "judge", 50m, BudgetPeriod.Monthly, 80, When));
    }

    [Fact]
    public void Create_an_org_budget_requires_a_valid_guid_scope_value()
    {
        var org = Guid.NewGuid();
        var b = AiUsageBudget.Create(BudgetScope.Organization, org.ToString(), 50m, BudgetPeriod.Monthly, 80, When);
        Assert.Equal(org.ToString(), b.ScopeValue);

        Assert.Throws<ArgumentException>(() =>
            AiUsageBudget.Create(BudgetScope.Organization, "not-a-guid", 50m, BudgetPeriod.Monthly, 80, When));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_rejects_a_non_positive_limit(decimal limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiUsageBudget.Create(BudgetScope.Global, null, limit, BudgetPeriod.Monthly, 80, When));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Create_rejects_an_out_of_range_threshold(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiUsageBudget.Create(BudgetScope.Global, null, 100m, BudgetPeriod.Monthly, threshold, When));
    }

    [Fact]
    public void Classify_returns_ok_warning_and_over()
    {
        var b = AiUsageBudget.Create(BudgetScope.Global, null, 100m, BudgetPeriod.Monthly, 80, When);

        Assert.Equal(BudgetStatusLevel.Ok, b.Classify(50m));       // 50%
        Assert.Equal(BudgetStatusLevel.Warning, b.Classify(80m));  // exactly at threshold
        Assert.Equal(BudgetStatusLevel.Warning, b.Classify(99.99m));
        Assert.Equal(BudgetStatusLevel.Over, b.Classify(100m));    // at/over limit
        Assert.Equal(BudgetStatusLevel.Over, b.Classify(150m));
    }
}
