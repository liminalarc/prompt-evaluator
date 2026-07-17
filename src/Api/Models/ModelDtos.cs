using Domain;

namespace Api.Models;

/// <summary>
/// A catalog model as seen by the SPA (spec 1.13). Roles are lowercase strings
/// (<c>subject</c>/<c>judge</c>/<c>generator</c>) so the web can filter droplists by role.
/// Provider-availability (<c>available</c>) is added in slice 3.
/// </summary>
public sealed record ModelResponse(
    Guid Id,
    string ModelId,
    string DisplayName,
    string Provider,
    IReadOnlyList<string> Roles,
    decimal? InputPricePerMTokUsd,
    decimal? OutputPricePerMTokUsd,
    bool IsActive)
{
    public static ModelResponse From(ModelCatalogEntry e) =>
        new(
            e.Id,
            e.ModelId,
            e.DisplayName,
            e.Provider.ToString(),
            e.Roles.Select(r => r.ToString().ToLowerInvariant()).ToList(),
            e.InputPricePerMTokUsd,
            e.OutputPricePerMTokUsd,
            e.IsActive);
}
