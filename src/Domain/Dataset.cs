namespace Domain;

/// <summary>
/// A named collection of <see cref="Fixture"/>s to evaluate prompts against. Capture-first:
/// fixtures are sampled from the apps and synthetic ones fill coverage gaps, seeded from
/// captured examples. Fixtures are append-only from the outside; origin is always recorded.
/// </summary>
public sealed class Dataset
{
    private readonly List<Fixture> _fixtures = new();

    public Guid Id { get; private set; }

    /// <summary>
    /// The prompt this dataset belongs to (1.7). A dataset lives with exactly one prompt — its
    /// fixtures are inputs shaped to that prompt's contract — so it always names its owner. A run
    /// against a dataset owned by a different prompt is rejected in the Application layer.
    /// </summary>
    public Guid PromptId { get; private set; }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>All fixtures in the dataset. Append-only from the outside.</summary>
    public IReadOnlyList<Fixture> Fixtures => _fixtures.AsReadOnly();

    private Dataset(Guid id, Guid promptId, string name, string? description)
    {
        Id = id;
        PromptId = promptId;
        Name = name;
        Description = description;
    }

    // Required by EF Core materialization; not for application use.
    private Dataset()
    {
        Name = string.Empty;
    }

    public static Dataset Create(Guid promptId, string name, string? description = null)
    {
        if (promptId == Guid.Empty)
            throw new ArgumentException("A dataset must belong to a prompt.", nameof(promptId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dataset name must not be blank.", nameof(name));

        return new Dataset(Guid.NewGuid(), promptId, name, Normalize(description));
    }

    /// <summary>Appends a captured (ground-truth) fixture.</summary>
    public Fixture AddCapturedFixture(
        string input,
        DateTimeOffset createdAt,
        string? label = null,
        string? description = null,
        string? upstreamContext = null,
        string? expectedOutput = null)
    {
        var fixture = new Fixture(
            FixtureOrigin.Captured,
            RequireInput(input),
            Normalize(label),
            Normalize(description),
            Normalize(upstreamContext),
            Normalize(expectedOutput),
            seedFixtureId: null,
            createdAt);
        _fixtures.Add(fixture);
        return fixture;
    }

    /// <summary>
    /// Appends a synthetic fixture, linked to the captured <paramref name="seedFixtureId"/> it
    /// was generated from. Generated synthetic fixtures must always name a seed so the distribution
    /// stays traceable to captured ground truth. (Hand-authored synthetic fixtures use
    /// <see cref="AddManualFixture"/> instead — they have no seed.)
    /// </summary>
    public Fixture AddSyntheticFixture(
        string input,
        Guid seedFixtureId,
        DateTimeOffset createdAt,
        string? label = null,
        string? description = null,
        string? upstreamContext = null,
        string? expectedOutput = null)
    {
        if (seedFixtureId == Guid.Empty)
            throw new ArgumentException("A synthetic fixture must name the seed it was generated from.", nameof(seedFixtureId));

        var fixture = new Fixture(
            FixtureOrigin.Synthetic,
            RequireInput(input),
            Normalize(label),
            Normalize(description),
            Normalize(upstreamContext),
            Normalize(expectedOutput),
            seedFixtureId,
            createdAt);
        _fixtures.Add(fixture);
        return fixture;
    }

    /// <summary>
    /// Appends a <b>hand-authored</b> fixture with an operator-chosen <paramref name="origin"/>
    /// (U8 — manual entry can mark Synthetic, not only Captured). Unlike a generated synthetic
    /// fixture it has no seed: the operator wrote it, so there is no captured example it derives
    /// from.
    /// </summary>
    public Fixture AddManualFixture(
        FixtureOrigin origin,
        string input,
        DateTimeOffset createdAt,
        string? label = null,
        string? description = null,
        string? upstreamContext = null,
        string? expectedOutput = null)
    {
        var fixture = new Fixture(
            origin,
            RequireInput(input),
            Normalize(label),
            Normalize(description),
            Normalize(upstreamContext),
            Normalize(expectedOutput),
            seedFixtureId: null,
            createdAt);
        _fixtures.Add(fixture);
        return fixture;
    }

    /// <summary>
    /// Edits a fixture's editable metadata — its label and description (U7). Input/origin/seed are
    /// fixed. Returns false when the fixture is not in this dataset.
    /// </summary>
    public bool EditFixtureMetadata(Guid fixtureId, string? label, string? description)
    {
        var fixture = _fixtures.FirstOrDefault(f => f.Id == fixtureId);
        if (fixture is null)
            return false;

        fixture.SetMetadata(Normalize(label), Normalize(description));
        return true;
    }

    private static string RequireInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Fixture input must not be blank.", nameof(input));
        return input;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
