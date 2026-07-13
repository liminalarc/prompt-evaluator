namespace Domain;

/// <summary>
/// The top-level container in the registry hierarchy (1.9): an organization owns a folder tree and
/// the prompts within it. It is the <b>permission boundary</b> — 4.1 grants multi-user access per
/// organization, resolved directly from <see cref="Prompt.OrganizationId"/>. An organization is not
/// itself a folder; it sits above the folder tree.
/// </summary>
public sealed class Organization
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    private Organization(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    // Required by EF Core materialization; not for application use.
    private Organization()
    {
        Name = string.Empty;
    }

    public static Organization Create(string name)
        => new(Guid.NewGuid(), RequireName(name));

    public void Rename(string name) => Name = RequireName(name);

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name must not be blank.", nameof(name));
        return name;
    }
}
