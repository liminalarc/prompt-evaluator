using Application.Ports;
using Domain;

namespace Application.Folders;

/// <summary>
/// Creates a folder in an organization (1.9) — a top-level folder when no parent is named,
/// otherwise a subfolder of the named parent. Returns null when the organization or named parent
/// does not exist (Api → 404); throws <see cref="ArgumentException"/> (Api → 400) when the parent
/// belongs to a different organization.
/// </summary>
public sealed class CreateFolderHandler(IOrganizationRepository organizations, IFolderRepository folders)
{
    public async Task<Folder?> HandleAsync(
        Guid organizationId, string name, Guid? parentId, CancellationToken ct = default)
    {
        var org = await organizations.GetByIdAsync(organizationId, ct);
        if (org is null)
            return null;

        Folder folder;
        if (parentId is Guid pid)
        {
            var parent = await folders.GetByIdAsync(pid, ct);
            if (parent is null)
                return null;
            if (parent.OrganizationId != organizationId)
                throw new ArgumentException(
                    "Parent folder belongs to a different organization.", nameof(parentId));
            folder = Folder.CreateChild(organizationId, name, pid);
        }
        else
        {
            folder = Folder.CreateRoot(organizationId, name);
        }

        await folders.AddAsync(folder, ct);
        return folder;
    }
}
