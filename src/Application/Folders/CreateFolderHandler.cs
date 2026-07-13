using Application.Ports;
using Domain;

namespace Application.Folders;

/// <summary>
/// Creates a folder — a top-level folder when no parent is named, otherwise a subfolder of the
/// named parent. Returns null when the named parent does not exist (Api → 404).
/// </summary>
public sealed class CreateFolderHandler(IFolderRepository folders)
{
    public async Task<Folder?> HandleAsync(string name, Guid? parentId, CancellationToken ct = default)
    {
        Folder folder;
        if (parentId is Guid pid)
        {
            var parent = await folders.GetByIdAsync(pid, ct);
            if (parent is null)
                return null;
            folder = Folder.CreateChild(name, pid);
        }
        else
        {
            folder = Folder.CreateRoot(name);
        }

        await folders.AddAsync(folder, ct);
        return folder;
    }
}
