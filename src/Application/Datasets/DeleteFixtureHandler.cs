using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>
/// Deletes a single fixture from a dataset (U19 — the recovery path for a mislabeled origin, since
/// origin is immutable: delete + re-add). The dataset's other fixtures, scorers and runs are
/// untouched. Returns null when the dataset or the fixture does not exist so the Api can translate
/// that to a 404.
/// </summary>
public sealed class DeleteFixtureHandler(IDatasetRepository repository)
{
    public async Task<Dataset?> HandleAsync(Guid datasetId, Guid fixtureId, CancellationToken ct = default)
    {
        var dataset = await repository.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            return null;

        if (!dataset.RemoveFixture(fixtureId))
            return null;

        await repository.SaveChangesAsync(ct);
        return dataset;
    }
}
