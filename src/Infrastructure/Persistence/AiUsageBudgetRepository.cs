using Application.AiUsage;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>EF-backed <see cref="IAiUsageBudgetRepository"/> (6.1.T6).</summary>
public sealed class AiUsageBudgetRepository(EvalDbContext db) : IAiUsageBudgetRepository
{
    public async Task AddAsync(AiUsageBudget budget, CancellationToken ct = default)
    {
        db.Set<AiUsageBudget>().Add(budget);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AiUsageBudget>> ListAsync(CancellationToken ct = default)
        => await db.Set<AiUsageBudget>().OrderBy(b => b.Scope).ThenBy(b => b.ScopeValue).ToListAsync(ct);

    public async Task<AiUsageBudget?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Set<AiUsageBudget>().FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<bool> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var budget = await db.Set<AiUsageBudget>().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (budget is null)
            return false;
        db.Remove(budget);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
