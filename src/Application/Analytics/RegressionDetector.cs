using MathNet.Numerics.Distributions;

namespace Application.Analytics;

/// <summary>
/// Flags regressions between consecutive prompt versions for a single scorer. A version is flagged
/// only when <b>both</b> hold vs. the prior version: (a) the mean score dropped by more than
/// <see cref="DefaultThreshold"/> (configurable), and (b) a paired t-test over the matched
/// per-fixture deltas finds the drop significant at <see cref="DefaultAlpha"/>. Requiring
/// significance keeps noisy series (especially LLM-judge) from false-flagging. Pure — no I/O.
/// </summary>
public sealed class RegressionDetector
{
    /// <summary>Minimum mean-score drop (on the normalized [0,1] scale) to consider a regression.</summary>
    public const double DefaultThreshold = 0.05;

    /// <summary>Significance level for the one-sided paired t-test.</summary>
    public const double DefaultAlpha = 0.05;

    /// <param name="versionsAscending">Version score sets ordered by version number ascending.</param>
    public IReadOnlyList<RegressionFlag> Detect(
        ScorerRef scorer,
        IReadOnlyList<VersionScoreSet> versionsAscending,
        double threshold = DefaultThreshold,
        double alpha = DefaultAlpha)
    {
        var flags = new List<RegressionFlag>();

        for (var i = 1; i < versionsAscending.Count; i++)
        {
            var prior = versionsAscending[i - 1];
            var current = versionsAscending[i];

            // Match fixtures present in both versions — a like-for-like paired comparison.
            var priorValues = new List<double>();
            var currentValues = new List<double>();
            foreach (var (fixtureId, priorValue) in prior.ScoresByFixture)
            {
                if (current.ScoresByFixture.TryGetValue(fixtureId, out var currentValue))
                {
                    priorValues.Add(priorValue);
                    currentValues.Add(currentValue);
                }
            }

            if (priorValues.Count == 0)
                continue;

            var priorMean = priorValues.Average();
            var currentMean = currentValues.Average();
            var drop = priorMean - currentMean;
            if (drop <= threshold)
                continue;

            var pValue = OneSidedDropPValue(priorValues, currentValues);
            if (pValue is null || pValue >= alpha)
                continue;

            flags.Add(new RegressionFlag(
                scorer,
                prior.VersionId, prior.VersionNumber, prior.VersionLabel,
                current.VersionId, current.VersionNumber, current.VersionLabel,
                priorMean, currentMean, currentMean - priorMean,
                pValue, priorValues.Count));
        }

        return flags;
    }

    /// <summary>
    /// One-sided (lower-tail) p-value of a paired t-test on the per-fixture deltas
    /// <c>current − prior</c>, i.e. the probability of a mean delta this negative under the null of
    /// no change. Returns null when fewer than two pairs (significance can't be established). A
    /// consistent nonzero drop with zero variance is treated as maximally significant.
    /// </summary>
    private static double? OneSidedDropPValue(IReadOnlyList<double> prior, IReadOnlyList<double> current)
    {
        var n = prior.Count;
        if (n < 2)
            return null;

        var deltas = new double[n];
        for (var i = 0; i < n; i++)
            deltas[i] = current[i] - prior[i];

        var meanDelta = deltas.Average();
        var variance = deltas.Sum(d => (d - meanDelta) * (d - meanDelta)) / (n - 1);

        if (variance <= 0)
            return meanDelta < 0 ? 0.0 : (meanDelta > 0 ? 1.0 : 0.5);

        var standardError = Math.Sqrt(variance / n);
        var t = meanDelta / standardError;
        return StudentT.CDF(location: 0.0, scale: 1.0, freedom: n - 1, t);
    }
}
