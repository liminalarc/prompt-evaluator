using Domain;

namespace Application.Ports;

/// <summary>
/// Persistence port for the <see cref="ModelCatalogEntry"/> aggregate — the workspace-wide Model
/// Catalog (spec 1.13). An EF Core (Postgres) adapter in Infrastructure implements it.
/// </summary>
public interface IModelCatalogRepository
{
    Task AddAsync(ModelCatalogEntry entry, CancellationToken ct = default);

    Task<ModelCatalogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The catalog, ordered by display name. Active entries only unless <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<ModelCatalogEntry>> ListAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Whether an entry with this (unique) model id already exists — guards create.</summary>
    Task<bool> ModelIdExistsAsync(string modelId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
