using Application.Ports;
using Domain;

namespace Application.Models;

/// <summary>
/// Edits a catalog entry's mutable fields (admin management, 1.13). The model id is immutable.
/// Returns null when no entry with that id exists (→ 404 at the edge).
/// </summary>
public sealed class UpdateModelHandler(IModelCatalogRepository repository)
{
    public async Task<ModelCatalogEntry?> HandleAsync(
        Guid id,
        string displayName,
        string provider,
        IEnumerable<string> roles,
        decimal? inputPricePerMTokUsd,
        decimal? outputPricePerMTokUsd,
        CancellationToken ct = default)
    {
        var entry = await repository.GetByIdAsync(id, ct);
        if (entry is null)
            return null;

        entry.Update(
            displayName,
            ModelCatalogInput.ParseProvider(provider),
            ModelCatalogInput.ParseRoles(roles),
            inputPricePerMTokUsd,
            outputPricePerMTokUsd);

        await repository.SaveChangesAsync(ct);
        return entry;
    }
}
