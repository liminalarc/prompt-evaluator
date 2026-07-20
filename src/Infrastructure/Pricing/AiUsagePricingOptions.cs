namespace Infrastructure.Pricing;

/// <summary>
/// The versioned per-model pricing table for the AI-usage ledger (6.1.T2), bound from the
/// <c>AiUsagePricing</c> config section over the built-in <see cref="Defaults"/>. USD per million
/// tokens per model. Cache rates follow Anthropic's model: cache-write ≈ 1.25× input (5-min), cache-read
/// ≈ 0.1× input; OpenAI has no cache-write premium and cached input ≈ 0.5× input. Rates confirmed
/// against the claude-api skill's model table at authoring time (2026-07); update the version on change.
/// </summary>
public sealed class AiUsagePricingOptions
{
    public string RateVersion { get; set; } = "unset";

    public Dictionary<string, ModelRate> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public sealed class ModelRate
    {
        public decimal InputPerMTokUsd { get; set; }
        public decimal OutputPerMTokUsd { get; set; }
        public decimal CacheWritePerMTokUsd { get; set; }
        public decimal CacheReadPerMTokUsd { get; set; }
    }

    /// <summary>The built-in table; config may override the version and any per-model rates.</summary>
    public static AiUsagePricingOptions Defaults() => new()
    {
        RateVersion = "2026-07",
        Models = new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-fable-5"] = new() { InputPerMTokUsd = 10m, OutputPerMTokUsd = 50m, CacheWritePerMTokUsd = 12.5m, CacheReadPerMTokUsd = 1.0m },
            ["claude-opus-4-8"] = new() { InputPerMTokUsd = 5m, OutputPerMTokUsd = 25m, CacheWritePerMTokUsd = 6.25m, CacheReadPerMTokUsd = 0.5m },
            ["claude-opus-4-7"] = new() { InputPerMTokUsd = 5m, OutputPerMTokUsd = 25m, CacheWritePerMTokUsd = 6.25m, CacheReadPerMTokUsd = 0.5m },
            ["claude-sonnet-5"] = new() { InputPerMTokUsd = 3m, OutputPerMTokUsd = 15m, CacheWritePerMTokUsd = 3.75m, CacheReadPerMTokUsd = 0.3m },
            ["claude-haiku-4-5"] = new() { InputPerMTokUsd = 1m, OutputPerMTokUsd = 5m, CacheWritePerMTokUsd = 1.25m, CacheReadPerMTokUsd = 0.1m },
            ["gpt-4o"] = new() { InputPerMTokUsd = 2.5m, OutputPerMTokUsd = 10m, CacheWritePerMTokUsd = 2.5m, CacheReadPerMTokUsd = 1.25m },
            ["gpt-4o-mini"] = new() { InputPerMTokUsd = 0.15m, OutputPerMTokUsd = 0.60m, CacheWritePerMTokUsd = 0.15m, CacheReadPerMTokUsd = 0.075m },
        },
    };

    /// <summary>Copies the defaults into this instance (used to seed the options before config binding).</summary>
    public void SeedDefaults()
    {
        var d = Defaults();
        RateVersion = d.RateVersion;
        Models = d.Models;
    }
}
