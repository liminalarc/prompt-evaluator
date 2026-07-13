using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class EvalRunRepository(EvalDbContext db) : IEvalRunRepository
{
    public async Task AddAsync(EvalRun run, CancellationToken ct = default)
    {
        await db.EvalRuns.AddAsync(run, ct);
        await db.SaveChangesAsync(ct);
    }

    // Owned collections (fixture_runs → scores) load eagerly with the run — no explicit Include.
    public Task<EvalRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.EvalRuns.SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<EvalRun>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default)
        => await db.EvalRuns
            .Where(r => r.DatasetId == datasetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EvalRun>> ListByPromptAndDatasetAsync(
        Guid promptId, Guid datasetId, CancellationToken ct = default)
        => await db.EvalRuns
            .Where(r => r.PromptId == promptId && r.DatasetId == datasetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
}
