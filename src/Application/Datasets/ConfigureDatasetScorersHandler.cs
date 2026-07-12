using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>
/// Adds a scorer to a dataset's persisted scorer set. Every <see cref="EvalRun"/> over the dataset
/// then scores with this set, so version comparisons stay like-for-like. Returns null when the
/// dataset does not exist (Api → 404).
/// </summary>
public sealed class ConfigureDatasetScorersHandler(
    IDatasetRepository datasets,
    IScorerConfigRepository scorerConfigs,
    TimeProvider time)
{
    public async Task<ScorerConfig?> HandleAsync(
        Guid datasetId, ScorerDescriptor descriptor, CancellationToken ct = default)
    {
        var dataset = await datasets.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            return null;

        var config = ScorerConfig.Create(datasetId, descriptor, time.GetUtcNow());
        await scorerConfigs.AddAsync(config, ct);
        return config;
    }

    public Task<IReadOnlyList<ScorerConfig>> ListAsync(Guid datasetId, CancellationToken ct = default)
        => scorerConfigs.ListByDatasetAsync(datasetId, ct);
}
