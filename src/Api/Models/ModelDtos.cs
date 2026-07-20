using Application.Ports;
using Domain;

namespace Api.Models;

/// <summary>Admin create payload (1.13). Provider/roles are strings, parsed to enums server-side.</summary>
public sealed record CreateModelRequest(
    string ModelId,
    string DisplayName,
    string Provider,
    IReadOnlyList<string> Roles,
    decimal? InputPricePerMTokUsd,
    decimal? OutputPricePerMTokUsd);

/// <summary>Admin edit payload (1.13). The model id is immutable, so it is not part of the body.</summary>
public sealed record UpdateModelRequest(
    string DisplayName,
    string Provider,
    IReadOnlyList<string> Roles,
    decimal? InputPricePerMTokUsd,
    decimal? OutputPricePerMTokUsd);

/// <summary>
/// A catalog model as seen by the SPA (spec 1.13). Roles are lowercase strings
/// (<c>subject</c>/<c>judge</c>/<c>generator</c>) so the web can filter droplists by role.
/// <c>available</c> reflects whether the eval-runner has configured credentials for the model's
/// provider — the web marks unavailable models rather than offering them.
///
/// <para><b>Pricing (6.2).</b> <see cref="InputPricePerMTokUsd"/> / <see cref="OutputPricePerMTokUsd"/>
/// remain the entry's <i>optional per-model override</i> (kept, never dropped — author intent). The
/// <b>displayed</b> price is <see cref="EffectiveInputPricePerMTokUsd"/> /
/// <see cref="EffectiveOutputPricePerMTokUsd"/> = <c>override ?? authoritative-table rate</c>, sourced
/// from the same 6.1 <c>AiUsagePricingOptions</c> the ledger charges against, so catalog and ledger show
/// one number. <see cref="PriceSource"/> says where the displayed price came from
/// (<c>override</c> / <c>table</c> / <c>none</c>).</para>
/// </summary>
public sealed record ModelResponse(
    Guid Id,
    string ModelId,
    string DisplayName,
    string Provider,
    IReadOnlyList<string> Roles,
    decimal? InputPricePerMTokUsd,
    decimal? OutputPricePerMTokUsd,
    decimal? EffectiveInputPricePerMTokUsd,
    decimal? EffectiveOutputPricePerMTokUsd,
    string PriceSource,
    bool IsActive,
    bool Available)
{
    /// <summary>
    /// Maps a catalog entry to the DTO, resolving availability against the eval-runner's configured
    /// providers and the displayed price against the authoritative pricing table (6.2).
    /// <paramref name="configuredProviders"/> is null when the eval-runner is unreachable — availability
    /// is then unknown, so the model is treated as available (not hidden). <paramref name="pricing"/>
    /// supplies the authoritative table rate; the entry's own price columns override it when present.
    /// </summary>
    public static ModelResponse From(
        ModelCatalogEntry e, IReadOnlyList<string>? configuredProviders, IUsagePricing pricing)
    {
        var rate = pricing.GetRate(e.ModelId);
        var hasOverride = e.InputPricePerMTokUsd is not null || e.OutputPricePerMTokUsd is not null;
        var effectiveInput = e.InputPricePerMTokUsd ?? rate?.InputPerMTokUsd;
        var effectiveOutput = e.OutputPricePerMTokUsd ?? rate?.OutputPerMTokUsd;
        var source = hasOverride ? "override" : rate is not null ? "table" : "none";

        return new(
            e.Id,
            e.ModelId,
            e.DisplayName,
            e.Provider.ToString(),
            e.Roles.Select(r => r.ToString().ToLowerInvariant()).ToList(),
            e.InputPricePerMTokUsd,
            e.OutputPricePerMTokUsd,
            effectiveInput,
            effectiveOutput,
            source,
            e.IsActive,
            configuredProviders is null || configuredProviders.Contains(WireName(e.Provider)));
    }

    // The eval-runner reports providers by their routing name (lower-case), so map the enum to match.
    private static string WireName(ModelProvider provider) => provider switch
    {
        ModelProvider.Anthropic => "anthropic",
        ModelProvider.OpenAi => "openai",
        _ => provider.ToString().ToLowerInvariant(),
    };
}
