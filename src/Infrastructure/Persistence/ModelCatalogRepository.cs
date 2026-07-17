using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ModelCatalogRepository(EvalDbContext db) : IModelCatalogRepository
{
    public async Task AddAsync(ModelCatalogEntry entry, CancellationToken ct = default)
    {
        await db.ModelCatalogEntries.AddAsync(entry, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<ModelCatalogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.ModelCatalogEntries.SingleOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<ModelCatalogEntry>> ListAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.ModelCatalogEntries.AsQueryable();
        if (!includeInactive)
            query = query.Where(e => e.IsActive);
        return await query.OrderBy(e => e.DisplayName).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
