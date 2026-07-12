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

    /// <summary>The full version history, oldest first. Append-only from the outside.</summary>
    public IReadOnlyList<PromptVersion> Versions => _versions.AsReadOnly();

    private Prompt(Guid id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    // Required by EF Core materialization; not for application use.
    private Prompt()
    {
        Name = string.Empty;
    }

    public static Prompt Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Prompt name must not be blank.", nameof(name));

        return new Prompt(Guid.NewGuid(), name, Normalize(description));
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
