using Application.Ports;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class PromptRepository(EvalDbContext db) : IPromptRepository
{
    public async Task AddAsync(Prompt prompt, CancellationToken ct = default)
    {
        await db.Prompts.AddAsync(prompt, ct);
        await db.SaveChangesAsync(ct);
    }

    // Owned collections load eagerly with their owner, so the version history comes back
    // without an explicit Include.
    public Task<Prompt?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Prompts.SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Prompt>> ListAsync(CancellationToken ct = default)
        => await db.Prompts.ToListAsync(ct);

    public async Task<IReadOnlyList<Prompt>> ListByFolderAsync(Guid? folderId, CancellationToken ct = default)
        => await db.Prompts.Where(p => p.FolderId == folderId).ToListAsync(ct);

    public async Task<IReadOnlyList<Prompt>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => await db.Prompts.Where(p => p.OrganizationId == organizationId).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Datasets cascade to the prompt via their FK (→ fixtures, owned), and versions are owned by
        // the prompt, so the DB removes both. But eval_runs and scorer_configs reference the prompt /
        // its datasets by id with no FK, so cascade them explicitly first. All in one transaction so
        // a failure part-way leaves nothing half-deleted.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var datasetIds = await db.Datasets
            .Where(d => d.PromptId == id)
            .Select(d => d.Id)
            .ToListAsync(ct);

        // eval_runs → fixture_runs → scores cascade at the DB level once the run rows go.
        await db.EvalRuns
            .Where(r => r.PromptId == id || datasetIds.Contains(r.DatasetId))
            .ExecuteDeleteAsync(ct);
        await db.ScorerConfigs
            .Where(c => datasetIds.Contains(c.DatasetId))
            .ExecuteDeleteAsync(ct);
        await db.Datasets.Where(d => d.PromptId == id).ExecuteDeleteAsync(ct);
        await db.Prompts.Where(p => p.Id == id).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }
}
