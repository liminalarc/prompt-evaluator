namespace Domain;

/// <summary>
/// One scorer's verdict on one fixture's output within an <see cref="EvalRun"/>. Immutable and
/// created only by the owning <see cref="FixtureRun"/>. <see cref="Value"/> is normalized to
/// [0, 1] so every scorer — deterministic pass/fail, fuzzy ratio, latency/cost budget, or
/// LLM-judge rating — is directly comparable.
/// </summary>
public sealed class Score
{
    public Guid Id { get; private set; }

    /// <summary>The scorer that produced this value — its identity anchors the score series.</summary>
    public ScorerDescriptor Scorer { get; private set; }

    /// <summary>Normalized score in [0, 1].</summary>
    public double Value { get; private set; }

    /// <summary>Optional pass/fail interpretation, where a scorer has a natural threshold.</summary>
    public bool? Passed { get; private set; }

    /// <summary>Optional human-readable detail (judge rationale, raw latency/cost, mismatch info).</summary>
    public string? Detail { get; private set; }

    internal Score(ScorerDescriptor scorer, double value, bool? passed, string? detail)
    {
        Id = Guid.NewGuid();
        Scorer = scorer;
        Value = value;
        Passed = passed;
        Detail = detail;
    }

    // Required by EF Core materialization; not for application use.
    private Score()
    {
        Scorer = null!;
    }
}
