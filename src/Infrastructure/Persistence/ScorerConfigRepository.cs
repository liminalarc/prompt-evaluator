using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ScorerConfigRepository(EvalDbContext db) : IScorerConfigRepository
{
    public async Task AddAsync(ScorerConfig config, CancellationToken ct = default)
    {
        await db.ScorerConfigs.AddAsync(config, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScorerConfig>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default)
        => await db.ScorerConfigs
            .Where(c => c.DatasetId == datasetId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public Task<ScorerConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.ScorerConfigs.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
        => await db.ScorerConfigs.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
}
