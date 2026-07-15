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

    public async Task<IReadOnlyList<Dataset>> ListByPromptAsync(Guid promptId, CancellationToken ct = default)
        => await db.Datasets.Where(d => d.PromptId == promptId).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Fixtures are owned by the dataset (cascade at the DB level), but eval_runs and
        // scorer_configs reference it by id with no FK, so cascade them explicitly. eval_runs →
        // fixture_runs → scores cascade once the run rows go. One transaction for atomicity.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.EvalRuns.Where(r => r.DatasetId == id).ExecuteDeleteAsync(ct);
        await db.ScorerConfigs.Where(c => c.DatasetId == id).ExecuteDeleteAsync(ct);
        await db.Datasets.Where(d => d.Id == id).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }
}
