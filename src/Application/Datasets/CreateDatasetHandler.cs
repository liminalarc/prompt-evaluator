using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>
/// Registers a new (empty) dataset under a prompt. Fixtures are added via capture or generation.
/// A dataset belongs to exactly one prompt (1.7); returns null when that prompt does not exist
/// (Api → 404) so a dataset can never be orphaned from its owner.
/// </summary>
public sealed class CreateDatasetHandler(IPromptRepository prompts, IDatasetRepository datasets)
{
    public async Task<Dataset?> HandleAsync(
        Guid promptId, string name, string? description, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var dataset = Dataset.Create(promptId, name, description);
        await datasets.AddAsync(dataset, ct);
        return dataset;
    }
}
