namespace Domain;

/// <summary>
/// One immutable point in a <see cref="Prompt"/>'s history. Content is fixed at creation;
/// there is no mutation path — a change means a new version. Created only by the owning
/// <see cref="Prompt"/> aggregate via <see cref="Prompt.AddVersion"/>.
/// </summary>
public sealed class PromptVersion
{
    public Guid Id { get; private set; }

    /// <summary>1-based ordinal within the owning prompt's history.</summary>
    public int VersionNumber { get; private set; }

    public string Content { get; private set; }

    /// <summary>
    /// The subject model this prompt version is meant to run against — an opaque identifier
    /// (a Claude tier today, a non-Claude/SLM id later). Not validated against a live
    /// provider here; multi-provider execution is spec 1.5.
    /// </summary>
    public string TargetModel { get; private set; }

    public string? Label { get; private set; }

    /// <summary>Optional reference to the app this prompt was copied in from.</summary>
    public string? SourceApp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal PromptVersion(
        int versionNumber,
        string content,
        string targetModel,
        string? label,
        string? sourceApp,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        VersionNumber = versionNumber;
        Content = content;
        TargetModel = targetModel;
        Label = label;
        SourceApp = sourceApp;
        CreatedAt = createdAt;
    }

    // Required by EF Core materialization; not for application use.
    private PromptVersion()
    {
        Content = string.Empty;
        TargetModel = string.Empty;
    }

    /// <summary>
    /// Updates the version's editable <see cref="Label"/> — an optional description. Content and
    /// target model are immutable (they define the run's identity) and have no setter. Only the
    /// owning <see cref="Prompt"/> aggregate calls this, via <see cref="Prompt.EditVersionLabel"/>.
    /// </summary>
    internal void SetLabel(string? label) => Label = label;
}
