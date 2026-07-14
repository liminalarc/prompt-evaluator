using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class OrganizationRepository(EvalDbContext db) : IOrganizationRepository
{
    public async Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        await db.Organizations.AddAsync(organization, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Organizations.SingleOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default)
        => await db.Organizations.OrderBy(o => o.Name).ToListAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        // FK cascades remove the org's folders, prompts, and those prompts' datasets/fixtures.
        => await db.Organizations.Where(o => o.Id == id).ExecuteDeleteAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
