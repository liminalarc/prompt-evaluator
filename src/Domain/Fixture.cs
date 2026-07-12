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
        string? upstreamContext,
        string? expectedOutput,
        Guid? seedFixtureId,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Origin = origin;
        Input = input;
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
}
