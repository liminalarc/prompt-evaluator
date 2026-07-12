using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class DatasetRepository(EvalDbContext db) : IDatasetRepository
{
    public async Task AddAsync(Dataset dataset, CancellationToken ct = default)
    {
        await db.Datasets.AddAsync(dataset, ct);
        await db.SaveChangesAsync(ct);
    }

    // Owned collections load eagerly with their owner, so the fixtures come back without an
    // explicit Include.
    public Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Datasets.SingleOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default)
        => await db.Datasets.ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
