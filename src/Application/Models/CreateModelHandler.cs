using Application.Ports;
using Domain;

namespace Application.Models;

/// <summary>Adds a model to the catalog (admin management, 1.13). Rejects a duplicate model id.</summary>
public sealed class CreateModelHandler(IModelCatalogRepository repository)
{
    public async Task<ModelCatalogEntry> HandleAsync(
        string modelId,
        string displayName,
        string provider,
        IEnumerable<string> roles,
        decimal? inputPricePerMTokUsd,
        decimal? outputPricePerMTokUsd,
        CancellationToken ct = default)
    {
        var trimmedId = (modelId ?? string.Empty).Trim();
        if (await repository.ModelIdExistsAsync(trimmedId, ct))
            throw new ArgumentException($"A model with id '{trimmedId}' already exists.", nameof(modelId));

        var entry = ModelCatalogEntry.Create(
            trimmedId,
            displayName,
            ModelCatalogInput.ParseProvider(provider),
            ModelCatalogInput.ParseRoles(roles),
            inputPricePerMTokUsd,
            outputPricePerMTokUsd);

        await repository.AddAsync(entry, ct);
        return entry;
    }
}
