using Api.Models;
using Application.Ports;
using Domain;

namespace Api.Tests;

/// <summary>
/// 6.2 — the Model Catalog's <b>displayed</b> price derives from the authoritative 6.1 pricing table:
/// <c>override ?? table rate</c>. The entry's own price columns are kept as an optional per-model
/// override (author intent wins); otherwise the catalog shows the same rate the ledger charges.
/// Pure mapper tests — no DB.
/// </summary>
public sealed class ModelResponsePricingTests
{
    private sealed class FakePricing(Dictionary<string, UsageRate> rates) : IUsagePricing
    {
        public string RateVersion => "test";

        public UsageCost Compute(string model, int inputTokens, int outputTokens, int cacheCreationTokens, int cacheReadTokens)
            => throw new NotSupportedException();

        public UsageRate? GetRate(string model) => rates.TryGetValue(model, out var r) ? r : null;
    }

    private static ModelCatalogEntry Entry(string modelId, decimal? input = null, decimal? output = null) =>
        ModelCatalogEntry.Create(modelId, "Display", ModelProvider.Anthropic, new[] { ModelRole.Subject }, input, output);

    [Fact]
    public void Effective_price_uses_the_authoritative_table_rate_when_there_is_no_override()
    {
        var entry = Entry("claude-opus-4-8");
        var pricing = new FakePricing(new() { ["claude-opus-4-8"] = new UsageRate(5m, 25m, 6.25m, 0.5m) });

        var dto = ModelResponse.From(entry, null, pricing);

        Assert.Equal(5m, dto.EffectiveInputPricePerMTokUsd);
        Assert.Equal(25m, dto.EffectiveOutputPricePerMTokUsd);
        Assert.Equal("table", dto.PriceSource);
        // The override columns stay null (nothing was overridden) — no data invented.
        Assert.Null(dto.InputPricePerMTokUsd);
        Assert.Null(dto.OutputPricePerMTokUsd);
    }

    [Fact]
    public void A_per_model_override_wins_over_the_table_rate()
    {
        var entry = Entry("claude-opus-4-8", input: 2m, output: 8m);
        var pricing = new FakePricing(new() { ["claude-opus-4-8"] = new UsageRate(5m, 25m, 6.25m, 0.5m) });

        var dto = ModelResponse.From(entry, null, pricing);

        Assert.Equal(2m, dto.EffectiveInputPricePerMTokUsd);
        Assert.Equal(8m, dto.EffectiveOutputPricePerMTokUsd);
        Assert.Equal("override", dto.PriceSource);
        // The override columns are preserved exactly.
        Assert.Equal(2m, dto.InputPricePerMTokUsd);
        Assert.Equal(8m, dto.OutputPricePerMTokUsd);
    }

    [Fact]
    public void Effective_price_is_null_when_there_is_neither_an_override_nor_a_table_rate()
    {
        var entry = Entry("mystery-model");
        var pricing = new FakePricing(new());

        var dto = ModelResponse.From(entry, null, pricing);

        Assert.Null(dto.EffectiveInputPricePerMTokUsd);
        Assert.Null(dto.EffectiveOutputPricePerMTokUsd);
        Assert.Equal("none", dto.PriceSource);
    }
}
