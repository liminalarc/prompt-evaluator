namespace Application.Analytics;

/// <summary>
/// The weighted composite score (2.9): one "overall quality" number for a run, the weighted mean of
/// its per-scorer means. Every <see cref="Domain.Score.Value"/> is already normalized to [0,1], so the
/// composite is directly comparable. Weights come from the dataset's <see cref="Domain.ScorerConfig"/>
/// (keyed by <see cref="Domain.ScorerDescriptor.Identity"/>); a scorer with no current config row —
/// e.g. one reconfigured away since the run — falls back to <c>1.0</c> so historical runs still score.
/// Pure — no I/O. Reused by the composite trend series, the version change table, and the [[1.16]]
/// backport signal.
/// </summary>
public static class CompositeScoring
{
    /// <summary>The default weight for a scorer with no explicit configured weight.</summary>
    public const double DefaultWeight = 1.0;

    /// <summary>
    /// The weighted mean of <paramref name="meansByScorer"/> (scorer identity → its mean score),
    /// weighting each by <paramref name="weightByIdentity"/> (falling back to <see cref="DefaultWeight"/>).
    /// Dividing by the sum of the weights of the scorers <b>actually present</b> renormalizes
    /// automatically when scorers are added or removed. Returns null when there are no scorer means.
    /// </summary>
    public static double? WeightedComposite(
        IReadOnlyDictionary<string, double> meansByScorer,
        IReadOnlyDictionary<string, double> weightByIdentity)
    {
        if (meansByScorer.Count == 0)
            return null;

        var weightedSum = 0.0;
        var totalWeight = 0.0;
        foreach (var (identity, mean) in meansByScorer)
        {
            var weight = weightByIdentity.TryGetValue(identity, out var w) ? w : DefaultWeight;
            weightedSum += weight * mean;
            totalWeight += weight;
        }

        return totalWeight <= 0.0 ? null : weightedSum / totalWeight;
    }
}
