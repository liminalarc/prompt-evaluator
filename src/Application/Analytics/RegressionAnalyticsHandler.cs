using Application.Ports;

namespace Application.Analytics;

/// <summary>
/// Finds regressions for a prompt over a dataset. Projects each version's latest run into per-scorer
/// <see cref="VersionScoreSet"/>s (fixture id → value), then runs <see cref="RegressionDetector"/>
/// over the consecutive versions of every scorer series. Read-only over the append-only history.
/// </summary>
public sealed class RegressionAnalyticsHandler(
    IEvalRunRepository runs, IPromptRepository prompts, RegressionDetector detector)
{
    /// <summary>Returns null when the prompt does not exist (Api → 404); empty list when nothing regressed.</summary>
    public async Task<IReadOnlyList<RegressionFlag>?> HandleAsync(
        Guid promptId, Guid datasetId, double? threshold = null, double? alpha = null, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versions = prompt.Versions.ToDictionary(v => v.Id);
        var allRuns = await runs.ListByPromptAndDatasetAsync(promptId, datasetId, ct);
        var latestPerVersion = AnalyticsProjection.LatestRunPerVersion(allRuns, versions.Keys.ToHashSet());

        // scorer identity -> its ScorerRef + a VersionScoreSet per version.
        var byScorer = new Dictionary<string, (ScorerRef Scorer, List<VersionScoreSet> Sets)>();

        foreach (var run in latestPerVersion)
        {
            var version = versions[run.PromptVersionId];

            foreach (var (scorer, scores) in AnalyticsProjection.ByScorer(run))
            {
                var map = new Dictionary<Guid, double>();
                foreach (var score in scores)
                    map[score.FixtureId] = score.Value; // one score per fixture per scorer

                var set = new VersionScoreSet(version.Id, version.VersionNumber, version.Label, run.Id, map);
                if (!byScorer.TryGetValue(scorer.Identity, out var entry))
                    byScorer[scorer.Identity] = entry = (scorer, []);
                entry.Sets.Add(set);
            }
        }

        var t = threshold ?? RegressionDetector.DefaultThreshold;
        var a = alpha ?? RegressionDetector.DefaultAlpha;

        return byScorer.Values
            .SelectMany(e => detector.Detect(e.Scorer, e.Sets.OrderBy(s => s.VersionNumber).ToList(), t, a))
            .OrderBy(f => f.Scorer.Kind)
            .ThenBy(f => f.ToVersionNumber)
            .ToList();
    }
}
