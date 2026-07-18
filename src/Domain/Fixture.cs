namespace Domain;

/// <summary>
/// One input the harness can run a prompt against. Records the prompt's actual
/// <see cref="Input"/> plus optional <see cref="UpstreamContext"/> (e.g. the upstream SLM
/// output it was derived from) and an optional <see cref="ExpectedOutput"/> reference. Created
/// only by the owning <see cref="Dataset"/> aggregate.
/// </summary>
public sealed class Fixture
{
    public Guid Id { get; private set; }

    public FixtureOrigin Origin { get; private set; }

    /// <summary>Optional short human label for the scenario (e.g. "improving mid-handicapper") —
    /// shown in tables instead of the raw input. Editable metadata (U7).</summary>
    public string? Label { get; private set; }

    /// <summary>Optional longer description of the fixture. Editable metadata (U7).</summary>
    public string? Description { get; private set; }

    /// <summary>The prompt's actual input.</summary>
    public string Input { get; private set; }

    /// <summary>Optional upstream context, e.g. the SLM output this input was derived from.</summary>
    public string? UpstreamContext { get; private set; }

    /// <summary>Optional expected/reference output for scoring.</summary>
    public string? ExpectedOutput { get; private set; }

    /// <summary>
    /// For <see cref="FixtureOrigin.Synthetic"/> fixtures, the captured <see cref="Fixture"/>
    /// this one was seeded from. Always null for captured fixtures.
    /// </summary>
    public Guid? SeedFixtureId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal Fixture(
        FixtureOrigin origin,
        string input,
        string? label,
        string? description,
        string? upstreamContext,
        string? expectedOutput,
        Guid? seedFixtureId,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Origin = origin;
        Input = input;
        Label = label;
        Description = description;
        UpstreamContext = upstreamContext;
        ExpectedOutput = expectedOutput;
        SeedFixtureId = seedFixtureId;
        CreatedAt = createdAt;
    }

    // Required by EF Core materialization; not for application use.
    private Fixture()
    {
        Input = string.Empty;
    }

    /// <summary>
    /// Updates the fixture's editable metadata — its <see cref="Label"/> and
    /// <see cref="Description"/>. Input/origin/seed are fixed. Called only by the owning
    /// <see cref="Dataset"/> aggregate, via <see cref="Dataset.EditFixtureMetadata"/>.
    /// </summary>
    internal void SetMetadata(string? label, string? description)
    {
        Label = label;
        Description = description;
    }
}
