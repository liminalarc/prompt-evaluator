namespace Application.Analytics;

/// <summary>
/// A summary of a set of scores treated as repeated samples: their <see cref="Mean"/>, the spread
/// (<see cref="StdDev"/>, population standard deviation), how many samples, and the observed range.
/// A single sample yields <see cref="StdDev"/> = 0 (n=1 is graceful, not an error).
/// </summary>
public sealed record VarianceStat(double Mean, double StdDev, int SampleCount, double Min, double Max)
{
    /// <summary>Population std-dev summary over the values (empty → all-zero).</summary>
    public static VarianceStat From(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return new VarianceStat(0, 0, 0, 0, 0);
        var mean = values.Average();
        var variance = values.Average(v => (v - mean) * (v - mean));
        return new VarianceStat(mean, Math.Sqrt(variance), values.Count, values.Min(), values.Max());
    }
}

/// <summary>One fixture's score across a version's repeated runs, as mean ± spread.</summary>
public sealed record FixtureVariance(Guid FixtureId, VarianceStat Value);

/// <summary>
/// A version's score stability for one scorer over a dataset: the aggregate (mean of each run's
/// per-fixture mean, ± spread across the <see cref="RunCount"/> runs) plus the per-fixture spread.
/// </summary>
public sealed record VersionVariance(
    Guid PromptVersionId,
    int VersionNumber,
    string? VersionLabel,
    int RunCount,
    VarianceStat Aggregate,
    IReadOnlyList<FixtureVariance> Fixtures);

/// <summary>
/// Score-stability for one scorer across a prompt's versions over a dataset — the variance sibling
/// of <see cref="TrendSeries"/>. Unlike trends (latest run per version), this aggregates <b>all</b>
/// runs of each version so a run-to-run wobble reads as spread, not signal (R4 / spec 2.14).
/// </summary>
public sealed record ScorerVariance(ScorerRef Scorer, IReadOnlyList<VersionVariance> Versions);
