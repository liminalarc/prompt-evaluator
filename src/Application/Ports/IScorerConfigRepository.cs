using Domain;

namespace Application.Ports;

/// <summary>
/// Persistence port for <see cref="ScorerConfig"/> — the scorers selected for a dataset. Scorer
/// selection is persisted per dataset (not per run) so every run over a dataset scores with the
/// same set, keeping version comparisons like-for-like.
/// </summary>
public interface IScorerConfigRepository
{
    Task AddAsync(ScorerConfig config, CancellationToken ct = default);

    Task<IReadOnlyList<ScorerConfig>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default);

    /// <summary>Loads one scorer config, or null if none exists.</summary>
    Task<ScorerConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persists changes made to a tracked scorer config (e.g. a reconfigured descriptor).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Removes a scorer config from a dataset's set. No-op if it does not exist.</summary>
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
