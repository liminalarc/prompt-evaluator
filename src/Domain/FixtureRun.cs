namespace Domain;

/// <summary>
/// One fixture's execution within an <see cref="EvalRun"/>: the subject model's captured
/// <see cref="ModelOutput"/> plus the latency/cost of producing it, and the <see cref="Score"/>s
/// each configured scorer assigned to that output. Created only by the owning <see cref="EvalRun"/>.
/// </summary>
public sealed class FixtureRun
{
    private readonly List<Score> _scores = new();

    public Guid Id { get; private set; }

    /// <summary>The <c>Fixture</c> (from the dataset) this result is for.</summary>
    public Guid FixtureId { get; private set; }

    /// <summary>The subject model's output for the fixture — may legitimately be empty.</summary>
    public string ModelOutput { get; private set; }

    public int LatencyMs { get; private set; }

    /// <summary>Prompt (input) tokens the subject model consumed producing the output.</summary>
    public int InputTokens { get; private set; }

    /// <summary>Completion (output) tokens the subject model generated.</summary>
    public int OutputTokens { get; private set; }

    /// <summary>Cost in USD of producing the output, or null when unknown.</summary>
    public decimal? CostUsd { get; private set; }

    /// <summary>The scores for this output — one per scorer. Append-only from the outside.</summary>
    public IReadOnlyList<Score> Scores => _scores.AsReadOnly();

    internal FixtureRun(Guid fixtureId, string modelOutput, int latencyMs, int inputTokens, int outputTokens, decimal? costUsd)
    {
        Id = Guid.NewGuid();
        FixtureId = fixtureId;
        ModelOutput = modelOutput;
        LatencyMs = latencyMs;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
    }

    // Required by EF Core materialization; not for application use.
    private FixtureRun()
    {
        ModelOutput = string.Empty;
    }

    /// <summary>Appends a scorer's verdict for this fixture's output.</summary>
    public Score AddScore(ScorerDescriptor scorer, double value, bool? passed, string? detail)
    {
        ArgumentNullException.ThrowIfNull(scorer);
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Score value must be in [0, 1].");

        var score = new Score(scorer, value, passed, detail);
        _scores.Add(score);
        return score;
    }
}
