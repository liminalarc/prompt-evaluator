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
    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>All fixtures in the dataset. Append-only from the outside.</summary>
    public IReadOnlyList<Fixture> Fixtures => _fixtures.AsReadOnly();

    private Dataset(Guid id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    // Required by EF Core materialization; not for application use.
    private Dataset()
    {
        Name = string.Empty;
    }

    public static Dataset Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dataset name must not be blank.", nameof(name));

        return new Dataset(Guid.NewGuid(), name, Normalize(description));
    }

    /// <summary>Appends a captured (ground-truth) fixture.</summary>
    public Fixture AddCapturedFixture(
        string input,
        DateTimeOffset createdAt,
        string? upstreamContext = null,
        string? expectedOutput = null)
    {
        var fixture = new Fixture(
            FixtureOrigin.Captured,
            RequireInput(input),
            Normalize(upstreamContext),
            Normalize(expectedOutput),
            seedFixtureId: null,
            createdAt);
        _fixtures.Add(fixture);
        return fixture;
    }

    /// <summary>
    /// Appends a synthetic fixture, linked to the captured <paramref name="seedFixtureId"/> it
    /// was generated from. Synthetic fixtures must always name a seed so the distribution stays
    /// traceable to captured ground truth.
    /// </summary>
    public Fixture AddSyntheticFixture(
        string input,
        Guid seedFixtureId,
        DateTimeOffset createdAt,
        string? upstreamContext = null,
        string? expectedOutput = null)
    {
        if (seedFixtureId == Guid.Empty)
            throw new ArgumentException("A synthetic fixture must name the seed it was generated from.", nameof(seedFixtureId));

        var fixture = new Fixture(
            FixtureOrigin.Synthetic,
            RequireInput(input),
            Normalize(upstreamContext),
            Normalize(expectedOutput),
            seedFixtureId,
            createdAt);
        _fixtures.Add(fixture);
        return fixture;
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
