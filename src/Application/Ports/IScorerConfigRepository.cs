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
}
