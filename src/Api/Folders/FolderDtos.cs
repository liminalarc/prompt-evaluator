using Domain;

namespace Api.Folders;

public sealed record CreateFolderRequest(string Name, Guid? ParentId);

public sealed record RenameFolderRequest(string Name);

public sealed record MoveFolderRequest(Guid? ParentId);

/// <summary>A folder node in the tree. The client assembles the tree from the flat list via ParentId.</summary>
public sealed record FolderResponse(Guid Id, Guid? ParentId, string Name)
{
    public static FolderResponse From(Folder f) => new(f.Id, f.ParentId, f.Name);
}
