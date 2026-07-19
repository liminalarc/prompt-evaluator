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

    /// <summary>
    /// The organization this prompt belongs to (1.9) — the <b>permission boundary</b> 4.1 grants
    /// access on, resolved directly (no tree walk).
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// The folder this prompt is filed into (1.7), or null when unfiled (shown under the org root).
    /// The folder, when set, must belong to the same organization — enforced in the Application layer.
    /// </summary>
    public Guid? FolderId { get; private set; }

    /// <summary>
    /// The version this prompt's <b>source app is actually running</b> (1.16) — the "Current in source"
    /// marker. Exactly one version at a time (a single pointer); <c>null</c> until first set. Backport
    /// signals derive from it: a higher-scoring version above Current is <i>backport-eligible</i>.
    /// LitmusAI only <b>signals</b> — the backport itself is a human action in the source repo.
    /// </summary>
    public Guid? CurrentVersionId { get; private set; }

    /// <summary>Optional commit SHA recorded when Current was set/moved — provenance of what shipped.</summary>
    public string? CurrentVersionSha { get; private set; }

    /// <summary>When Current was last set/moved (null until first set).</summary>
    public DateTimeOffset? CurrentVersionSetAt { get; private set; }

    /// <summary>The full version history, oldest first. Append-only from the outside.</summary>
    public IReadOnlyList<PromptVersion> Versions
        => _versions.OrderBy(v => v.VersionNumber).ToList().AsReadOnly();

    private Prompt(Guid id, Guid organizationId, string name, string? description, Guid? folderId)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Description = description;
        FolderId = folderId;
    }

    // Required by EF Core materialization; not for application use.
    private Prompt()
    {
        Name = string.Empty;
    }

    public static Prompt Create(
        Guid organizationId, string name, string? description = null, Guid? folderId = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("A prompt must belong to an organization.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Prompt name must not be blank.", nameof(name));

        return new Prompt(Guid.NewGuid(), organizationId, name, Normalize(description), folderId);
    }

    /// <summary>Files (or re-files) this prompt into a folder.</summary>
    public void MoveToFolder(Guid folderId)
    {
        if (folderId == Guid.Empty)
            throw new ArgumentException("Folder id must not be empty.", nameof(folderId));

        FolderId = folderId;
    }

    /// <summary>Removes this prompt from its folder — it becomes unfiled (shown under the root).</summary>
    public void Unfile() => FolderId = null;

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

    /// <summary>
    /// Edits a version's <b>editable metadata</b> — its <see cref="PromptVersion.Label"/> (an
    /// optional description). Content and target model are <b>immutable</b> (they define the run's
    /// identity — change them by adding a new version), so they are never touched here. Returns
    /// false when the version is not in this prompt's history.
    /// </summary>
    public bool EditVersionLabel(Guid versionId, string? label)
    {
        var version = _versions.FirstOrDefault(v => v.Id == versionId);
        if (version is null)
            return false;

        version.SetLabel(Normalize(label));
        return true;
    }

    /// <summary>
    /// Marks a version as <b>Current in source</b> (1.16) — the one the app runs. This is the single
    /// mechanic behind both the initial marker and <i>mark-as-backported</i> (moving Current forward to
    /// a shipped, higher-scoring version); the eligibility flag re-derives against the new pointer.
    /// Records an optional commit SHA + the set timestamp. Returns false when the version is not in this
    /// prompt's history (nothing changes).
    /// </summary>
    public bool SetCurrentVersion(Guid versionId, string? commitSha, DateTimeOffset setAt)
    {
        if (_versions.All(v => v.Id != versionId))
            return false;

        CurrentVersionId = versionId;
        CurrentVersionSha = Normalize(commitSha);
        CurrentVersionSetAt = setAt;
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
