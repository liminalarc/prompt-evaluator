namespace Domain;

/// <summary>
/// A registered prompt with an append-only <see cref="PromptVersion"/> history. Prompts are
/// copied into the registry now; the <c>IPromptRepository</c> port leaves the door open for
/// Zatomic to become the backing store later without touching the domain. Score identity is
/// <c>Prompt × Version × Dataset × Scorer</c>, so a stable per-version identity is the point.
/// </summary>
public sealed class Prompt
{
    private readonly List<PromptVersion> _versions = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// The folder this prompt is filed into (1.7), or null when unfiled — an unfiled prompt is
    /// shown under the root folder. A prompt belongs to exactly one folder; the folder's top-level
    /// ancestor is the permission boundary (4.1).
    /// </summary>
    public Guid? FolderId { get; private set; }

    /// <summary>The full version history, oldest first. Append-only from the outside.</summary>
    public IReadOnlyList<PromptVersion> Versions
        => _versions.OrderBy(v => v.VersionNumber).ToList().AsReadOnly();

    private Prompt(Guid id, string name, string? description, Guid? folderId)
    {
        Id = id;
        Name = name;
        Description = description;
        FolderId = folderId;
    }

    // Required by EF Core materialization; not for application use.
    private Prompt()
    {
        Name = string.Empty;
    }

    public static Prompt Create(string name, string? description = null, Guid? folderId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Prompt name must not be blank.", nameof(name));

        return new Prompt(Guid.NewGuid(), name, Normalize(description), folderId);
    }

    /// <summary>Files (or re-files) this prompt into a folder.</summary>
    public void MoveToFolder(Guid folderId)
    {
        if (folderId == Guid.Empty)
            throw new ArgumentException("Folder id must not be empty.", nameof(folderId));

        FolderId = folderId;
    }

    /// <summary>
    /// Appends a new immutable version, assigning the next sequential <see cref="PromptVersion.VersionNumber"/>.
    /// Existing versions are never mutated or removed — the history only grows.
    /// </summary>
    public PromptVersion AddVersion(
        string content,
        string targetModel,
        DateTimeOffset createdAt,
        string? label = null,
        string? sourceApp = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Version content must not be blank.", nameof(content));
        if (string.IsNullOrWhiteSpace(targetModel))
            throw new ArgumentException("Target model must not be blank.", nameof(targetModel));

        var version = new PromptVersion(
            _versions.Count + 1, content, targetModel, Normalize(label), Normalize(sourceApp), createdAt);
        _versions.Add(version);
        return version;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
