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

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
