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
/// </summary>
public sealed record ModelResponse(
    Guid Id,
    string ModelId,
    string DisplayName,
    string Provider,
    IReadOnlyList<string> Roles,
    decimal? InputPricePerMTokUsd,
    decimal? OutputPricePerMTokUsd,
    bool IsActive,
    bool Available)
{
    /// <summary>
    /// Maps a catalog entry to the DTO, resolving availability against the eval-runner's configured
    /// providers. <paramref name="configuredProviders"/> is null when the eval-runner is unreachable
    /// — availability is then unknown, so the model is treated as available (not hidden).
    /// </summary>
    public static ModelResponse From(ModelCatalogEntry e, IReadOnlyList<string>? configuredProviders) =>
        new(
            e.Id,
            e.ModelId,
            e.DisplayName,
            e.Provider.ToString(),
            e.Roles.Select(r => r.ToString().ToLowerInvariant()).ToList(),
            e.InputPricePerMTokUsd,
            e.OutputPricePerMTokUsd,
            e.IsActive,
            configuredProviders is null || configuredProviders.Contains(WireName(e.Provider)));

    // The eval-runner reports providers by their routing name (lower-case), so map the enum to match.
    private static string WireName(ModelProvider provider) => provider switch
    {
        ModelProvider.Anthropic => "anthropic",
        ModelProvider.OpenAi => "openai",
        _ => provider.ToString().ToLowerInvariant(),
    };
}
