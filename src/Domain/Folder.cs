namespace Domain;

/// <summary>
/// A node in the prompt-organizing folder tree: a name and an optional parent (null parent =
/// top-level). Prompts are filed into folders; the <b>top-level ancestor is the permission
/// boundary</b> that 4.1 grants access on (subfolders inherit). The top-level ancestor is resolved
/// by walking <see cref="ParentId"/> in the repository (a recursive query) rather than a
/// denormalized root ref, so moving a folder is a single-row change with no descendant cascade.
/// </summary>
public sealed class Folder
{
    public Guid Id { get; private set; }

    /// <summary>The organization this folder belongs to (1.9) — the permission boundary for 4.1.</summary>
    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    /// <summary>The parent folder, or null for a top-level folder (within the organization).</summary>
    public Guid? ParentId { get; private set; }

    public bool IsTopLevel => ParentId is null;

    private Folder(Guid id, Guid organizationId, string name, Guid? parentId)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        ParentId = parentId;
    }

    // Required by EF Core materialization; not for application use.
    private Folder()
    {
        Name = string.Empty;
    }

    /// <summary>Creates a top-level folder within an organization.</summary>
    public static Folder CreateRoot(Guid organizationId, string name)
        => new(Guid.NewGuid(), RequireOrg(organizationId), RequireName(name), parentId: null);

    /// <summary>
    /// Creates a subfolder under <paramref name="parentId"/>. The caller must pass the parent's
    /// organization — the Application layer enforces that a subfolder shares its parent's org.
    /// </summary>
    public static Folder CreateChild(Guid organizationId, string name, Guid parentId)
    {
        if (parentId == Guid.Empty)
            throw new ArgumentException("A child folder must name its parent.", nameof(parentId));

        return new Folder(Guid.NewGuid(), RequireOrg(organizationId), RequireName(name), parentId);
    }

    public void Rename(string name) => Name = RequireName(name);

    /// <summary>
    /// Reparents this folder under <paramref name="newParentId"/>, or promotes it to top-level when
    /// null. Rejects the one cycle visible without the tree — making the folder its own parent;
    /// deeper cycle detection (moving under a descendant) is enforced in the Application layer,
    /// which has the tree.
    /// </summary>
    public void MoveTo(Guid? newParentId)
    {
        if (newParentId == Id)
            throw new ArgumentException("A folder cannot be its own parent.", nameof(newParentId));

        ParentId = newParentId;
    }

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name must not be blank.", nameof(name));
        return name;
    }

    private static Guid RequireOrg(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("A folder must belong to an organization.", nameof(organizationId));
        return organizationId;
    }
}
