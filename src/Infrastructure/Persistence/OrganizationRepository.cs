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
    {
        // Folders, prompts, and those prompts' datasets/fixtures/versions cascade via FKs when the
        // org goes. But eval_runs and scorer_configs reference their prompt/dataset by id with NO FK,
        // so they'd be orphaned — remove them explicitly first (mirrors PromptRepository), all in one
        // transaction. Deleting eval_runs cascades to their fixture_runs/scores via real FKs.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var promptIds = await db.Prompts
            .Where(p => p.OrganizationId == id)
            .Select(p => p.Id)
            .ToListAsync(ct);
        var datasetIds = await db.Datasets
            .Where(d => promptIds.Contains(d.PromptId))
            .Select(d => d.Id)
            .ToListAsync(ct);

        await db.EvalRuns
            .Where(r => promptIds.Contains(r.PromptId) || datasetIds.Contains(r.DatasetId))
            .ExecuteDeleteAsync(ct);
        await db.ScorerConfigs
            .Where(c => datasetIds.Contains(c.DatasetId))
            .ExecuteDeleteAsync(ct);
        await db.Organizations.Where(o => o.Id == id).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
