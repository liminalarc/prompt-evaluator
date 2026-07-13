using Domain;

namespace Application.Ports;

/// <summary>
/// Persistence port for the <see cref="Dataset"/> aggregate and its fixtures. An EF Core
/// (Postgres) adapter in Infrastructure implements it today; no domain type depends on the
/// storage choice.
/// </summary>
public interface IDatasetRepository
{
    /// <summary>Persists a newly created dataset, including any fixtures already added to it.</summary>
    Task AddAsync(Dataset dataset, CancellationToken ct = default);

    /// <summary>Loads a dataset with its full fixture list, or null if none exists.</summary>
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>All datasets with their fixtures, for browsing.</summary>
    Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default);

    /// <summary>The datasets belonging to a prompt (1.7) — the prompt's own test sets.</summary>
    Task<IReadOnlyList<Dataset>> ListByPromptAsync(Guid promptId, CancellationToken ct = default);

    /// <summary>Persists changes made to a tracked aggregate (e.g. newly appended fixtures).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
