namespace Application.Ports;

/// <summary>The cost of one model call, computed from the versioned pricing table (6.1.T2).</summary>
/// <param name="CostUsd">Cost in USD, or null when the model has no pricing entry.</param>
/// <param name="RateVersion">The pricing-table version the cost was computed against.</param>
/// <param name="PricingMissing">True when the model had no pricing entry.</param>
public readonly record struct UsageCost(decimal? CostUsd, string RateVersion, bool PricingMissing);

/// <summary>
/// The per-model rate from the authoritative pricing table (USD per million tokens). Exposed so
/// callers other than the cost snapshotter — e.g. the Model Catalog display price (6.2) — can read the
/// table's rate for a model without re-deriving it from token counts.
/// </summary>
public readonly record struct UsageRate(
    decimal InputPerMTokUsd,
    decimal OutputPerMTokUsd,
    decimal CacheWritePerMTokUsd,
    decimal CacheReadPerMTokUsd);

/// <summary>
/// The authoritative per-model pricing table for the AI-usage ledger (6.1.T2): USD per million tokens
/// for input / output / cache-write / cache-read, plus a table version. Config-backed; the eval-runner
/// <c>_PRICING</c> table and the Model Catalog display price are left as-is (their unification is 6.2).
/// </summary>
public interface IUsagePricing
{
    /// <summary>The current pricing-table version, snapshotted onto each record.</summary>
    string RateVersion { get; }

    /// <summary>Computes the cost of a call from its token counts; unknown model → cost null + flagged.</summary>
    UsageCost Compute(string model, int inputTokens, int outputTokens, int cacheCreationTokens, int cacheReadTokens);

    /// <summary>The authoritative table's rate for a model, or null when the model has no entry (6.2).</summary>
    UsageRate? GetRate(string model);
}
