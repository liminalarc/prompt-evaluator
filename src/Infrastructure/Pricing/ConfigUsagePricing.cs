using Application.Ports;
using Microsoft.Extensions.Options;

namespace Infrastructure.Pricing;

/// <summary>
/// <see cref="IUsagePricing"/> over the config-backed <see cref="AiUsagePricingOptions"/> (6.1.T2).
/// Cost = Σ(tokens / 1e6 × per-MTok rate) across input / output / cache-write / cache-read, rounded to
/// 6 dp. Unknown model → cost null + <see cref="UsageCost.PricingMissing"/> true (tokens still stored).
/// </summary>
public sealed class ConfigUsagePricing(IOptions<AiUsagePricingOptions> options) : IUsagePricing
{
    private readonly AiUsagePricingOptions _options = options.Value;

    public string RateVersion => _options.RateVersion;

    public UsageCost Compute(string model, int inputTokens, int outputTokens, int cacheCreationTokens, int cacheReadTokens)
    {
        if (!_options.Models.TryGetValue(model, out var rate))
            return new UsageCost(CostUsd: null, RateVersion: _options.RateVersion, PricingMissing: true);

        var cost =
            inputTokens / 1_000_000m * rate.InputPerMTokUsd
            + outputTokens / 1_000_000m * rate.OutputPerMTokUsd
            + cacheCreationTokens / 1_000_000m * rate.CacheWritePerMTokUsd
            + cacheReadTokens / 1_000_000m * rate.CacheReadPerMTokUsd;

        return new UsageCost(decimal.Round(cost, 6), _options.RateVersion, PricingMissing: false);
    }

    public UsageRate? GetRate(string model) =>
        _options.Models.TryGetValue(model, out var rate)
            ? new UsageRate(rate.InputPerMTokUsd, rate.OutputPerMTokUsd, rate.CacheWritePerMTokUsd, rate.CacheReadPerMTokUsd)
            : null;
}
