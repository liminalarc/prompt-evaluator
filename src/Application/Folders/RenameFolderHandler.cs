using Application.Ports;
using Domain;

namespace Application.Folders;

/// <summary>Renames a folder. Returns null when it does not exist (Api → 404).</summary>
public sealed class RenameFolderHandler(IFolderRepository folders)
{
    public async Task<Folder?> HandleAsync(Guid id, string name, CancellationToken ct = default)
    {
        var folder = await folders.GetByIdAsync(id, ct);
        if (folder is null)
            return null;

        folder.Rename(name);
        await folders.SaveChangesAsync(ct);
        return folder;
    }
}
