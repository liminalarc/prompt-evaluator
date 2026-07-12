using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>Registers a new (empty) dataset. Fixtures are added via capture or generation.</summary>
public sealed class CreateDatasetHandler(IDatasetRepository repository)
{
    public async Task<Dataset> HandleAsync(string name, string? description, CancellationToken ct = default)
    {
        var dataset = Dataset.Create(name, description);
        await repository.AddAsync(dataset, ct);
        return dataset;
    }
}
