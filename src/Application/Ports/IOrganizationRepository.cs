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

    Task SaveChangesAsync(CancellationToken ct = default);
}
