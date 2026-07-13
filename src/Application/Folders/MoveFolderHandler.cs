using Application.Ports;
using Domain;

namespace Application.Folders;

/// <summary>
/// Reparents a folder (or promotes it to top-level when the new parent is null). Returns null when
/// the folder does not exist (Api → 404); throws <see cref="ArgumentException"/> (Api → 400) when
/// the target parent is missing or the move would form a cycle (under itself or a descendant).
/// </summary>
public sealed class MoveFolderHandler(IFolderRepository folders)
{
    public async Task<Folder?> HandleAsync(Guid id, Guid? newParentId, CancellationToken ct = default)
    {
        var folder = await folders.GetByIdAsync(id, ct);
        if (folder is null)
            return null;

        if (newParentId is Guid pid)
        {
            var parent = await folders.GetByIdAsync(pid, ct);
            if (parent is null)
                throw new ArgumentException("Target parent folder does not exist.", nameof(newParentId));

            var descendants = await folders.GetDescendantIdsAsync(id, ct);
            if (descendants.Contains(pid))
                throw new ArgumentException(
                    "A folder cannot be moved under one of its own descendants.", nameof(newParentId));
        }

        folder.MoveTo(newParentId); // domain rejects making the folder its own parent
        await folders.SaveChangesAsync(ct);
        return folder;
    }
}
