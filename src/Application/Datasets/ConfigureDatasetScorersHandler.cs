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

    /// <summary>
    /// Replaces a scorer's descriptor in place (U9 edit). Returns null when the scorer does not
    /// exist or does not belong to <paramref name="datasetId"/> (Api → 404).
    /// </summary>
    public async Task<ScorerConfig?> ReconfigureAsync(
        Guid datasetId, Guid scorerId, ScorerDescriptor descriptor, CancellationToken ct = default)
    {
        var config = await scorerConfigs.GetByIdAsync(scorerId, ct);
        if (config is null || config.DatasetId != datasetId)
            return null;

        config.Reconfigure(descriptor);
        await scorerConfigs.SaveChangesAsync(ct);
        return config;
    }

    /// <summary>
    /// Removes a scorer from a dataset's set (U9). Returns false when it does not exist or does not
    /// belong to <paramref name="datasetId"/> (Api → 404).
    /// </summary>
    public async Task<bool> RemoveAsync(Guid datasetId, Guid scorerId, CancellationToken ct = default)
    {
        var config = await scorerConfigs.GetByIdAsync(scorerId, ct);
        if (config is null || config.DatasetId != datasetId)
            return false;

        await scorerConfigs.RemoveAsync(scorerId, ct);
        return true;
    }
}
