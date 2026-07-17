using Application.Ports;
using Domain;

namespace Application.Models;

/// <summary>
/// Activates or deactivates a catalog entry (admin management, 1.13). Deactivated entries stay for
/// history but drop out of the droplists. Returns null when no entry with that id exists (→ 404).
/// </summary>
public sealed class SetModelActiveHandler(IModelCatalogRepository repository)
{
    public async Task<ModelCatalogEntry?> HandleAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entry = await repository.GetByIdAsync(id, ct);
        if (entry is null)
            return null;

        if (isActive)
            entry.Activate();
        else
            entry.Deactivate();

        await repository.SaveChangesAsync(ct);
        return entry;
    }
}
