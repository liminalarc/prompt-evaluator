namespace Domain;

/// <summary>What an <see cref="AiUsageBudget"/> is scoped to (6.1.T6).</summary>
public enum BudgetScope { Global, Model, Feature, Organization }

/// <summary>The budget's tracking period (monthly to start — 6.1.T6).</summary>
public enum BudgetPeriod { Monthly }

/// <summary>Where current-period spend sits against a budget (6.1.T6).</summary>
public enum BudgetStatusLevel { Ok, Warning, Over }

/// <summary>
/// A spend budget for the AI-usage ledger (6.1.T6): a global (workspace) limit, or one scoped to a
/// model / feature / organization, tracked over a period. Tracking + alerting only — no hard
/// enforcement of the run path (explicitly out of scope; a follow-on).
/// </summary>
public sealed class AiUsageBudget
{
    public Guid Id { get; private set; }
    public BudgetScope Scope { get; private set; }

    /// <summary>The scoped value — model id / feature name / org id; null for <see cref="BudgetScope.Global"/>.</summary>
    public string? ScopeValue { get; private set; }

    public decimal LimitUsd { get; private set; }
    public BudgetPeriod Period { get; private set; }

    /// <summary>Percent-used at which a warning fires (1–100); over-budget (≥100%) always alerts.</summary>
    public int AlertThresholdPercent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AiUsageBudget()
    {
    }

    public static AiUsageBudget Create(
        BudgetScope scope,
        string? scopeValue,
        decimal limitUsd,
        BudgetPeriod period,
        int alertThresholdPercent,
        DateTimeOffset createdAt)
    {
        if (limitUsd <= 0)
            throw new ArgumentOutOfRangeException(nameof(limitUsd), limitUsd, "Budget limit must be positive.");
        if (alertThresholdPercent is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(alertThresholdPercent), alertThresholdPercent,
                "Alert threshold must be between 1 and 100.");

        if (scope == BudgetScope.Global)
        {
            if (!string.IsNullOrWhiteSpace(scopeValue))
                throw new ArgumentException("A global budget must not carry a scope value.", nameof(scopeValue));
            scopeValue = null;
        }
        else if (string.IsNullOrWhiteSpace(scopeValue))
        {
            throw new ArgumentException($"A {scope} budget requires a scope value.", nameof(scopeValue));
        }

        return new AiUsageBudget
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            ScopeValue = scopeValue,
            LimitUsd = limitUsd,
            Period = period,
            AlertThresholdPercent = alertThresholdPercent,
            CreatedAt = createdAt,
        };
    }

    /// <summary>Classifies current-period <paramref name="spendUsd"/> against this budget.</summary>
    public BudgetStatusLevel Classify(decimal spendUsd)
    {
        if (spendUsd >= LimitUsd)
            return BudgetStatusLevel.Over;
        var percent = (double)(spendUsd / LimitUsd) * 100.0;
        return percent >= AlertThresholdPercent ? BudgetStatusLevel.Warning : BudgetStatusLevel.Ok;
    }
}
