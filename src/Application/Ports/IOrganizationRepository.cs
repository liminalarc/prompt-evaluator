using Domain;

namespace Application.Ports;

/// <summary>
/// Persistence port for the <see cref="Organization"/> aggregate (1.9) — the top-level container
/// and 4.1 permission boundary. An EF Core (Postgres) adapter in Infrastructure implements it.
/// </summary>
public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken ct = default);

    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every organization — for the switcher (access-filtered by 4.1 later; all for now).</summary>
    Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes an organization and everything under it (its folders, prompts, and those prompts'
    /// datasets cascade via the FKs). No-op if it does not exist.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
