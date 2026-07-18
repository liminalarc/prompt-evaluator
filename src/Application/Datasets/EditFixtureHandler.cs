using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>
/// Edits a fixture's editable metadata (label + description — U7). Input/origin/seed are fixed.
/// Returns null when the dataset or the fixture does not exist so the Api can translate that to a 404.
/// </summary>
public sealed class EditFixtureHandler(IDatasetRepository repository)
{
    public async Task<Dataset?> HandleAsync(
        Guid datasetId, Guid fixtureId, string? label, string? description, CancellationToken ct = default)
    {
        var dataset = await repository.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            return null;

        if (!dataset.EditFixtureMetadata(fixtureId, label, description))
            return null;

        await repository.SaveChangesAsync(ct);
        return dataset;
    }
}
