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

    public Task<EvalRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.EvalRuns.SingleOrDefaultAsync(r => r.Id == id, ct);
}
