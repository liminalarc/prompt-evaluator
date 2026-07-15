using Domain;

namespace Application.Ports;

/// <summary>
/// Persistence port for the <see cref="Folder"/> tree (1.7). An EF Core (Postgres) adapter in
/// Infrastructure implements it today; no domain type depends on the storage choice.
/// </summary>
public interface IFolderRepository
{
    /// <summary>Persists a newly created folder.</summary>
    Task AddAsync(Folder folder, CancellationToken ct = default);

    /// <summary>Loads a folder, or null if none exists.</summary>
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every folder — the tree is small enough to materialize whole for browsing.</summary>
    Task<IReadOnlyList<Folder>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// The folders in an organization (1.9) — the org's whole tree. The default filters
    /// <see cref="ListAsync"/>; the EF adapter overrides it with a DB-side query.
    /// </summary>
    async Task<IReadOnlyList<Folder>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => (await ListAsync(ct)).Where(f => f.OrganizationId == organizationId).ToList();

    /// <summary>
    /// The top-level ancestor of a folder — the permission boundary 4.1 grants access on. Returns
    /// the folder's own id when it is already top-level, or null if the folder does not exist.
    /// Resolved by walking parents, so moving a folder stays a single-row change with no cascade.
    /// </summary>
    Task<Guid?> GetTopLevelAncestorIdAsync(Guid folderId, CancellationToken ct = default);

    /// <summary>
    /// The ids of every folder beneath this one (its whole subtree, excluding itself). Used to
    /// reject moving a folder under one of its own descendants, which would form a cycle.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken ct = default);

    /// <summary>Persists changes made to a tracked folder (e.g. a rename or move).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a folder after <b>reparenting</b> its contents (1.10) — its child folders and filed
    /// prompts move up to this folder's parent (or the organization root when it is top-level), the
    /// least destructive option. No-op if the folder does not exist.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
