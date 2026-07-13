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
        var latestPerVersion = allRuns
            .Where(r => versions.ContainsKey(r.PromptVersionId))
            .GroupBy(r => r.PromptVersionId)
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First());

        // scorer identity -> its ScorerRef + a VersionScoreSet per version.
        var byScorer = new Dictionary<string, (ScorerRef Scorer, List<VersionScoreSet> Sets)>();

        foreach (var run in latestPerVersion)
        {
            var version = versions[run.PromptVersionId];
            var scoresByScorer = run.Results
                .SelectMany(fr => fr.Scores.Select(s => (fr.FixtureId, Score: s)))
                .GroupBy(x => x.Score.Scorer.Identity);

            foreach (var group in scoresByScorer)
            {
                var items = group.ToList();
                var map = new Dictionary<Guid, double>();
                foreach (var (fixtureId, score) in items)
                    map[fixtureId] = score.Value; // one score per fixture per scorer

                var set = new VersionScoreSet(version.Id, version.VersionNumber, version.Label, run.Id, map);
                if (!byScorer.TryGetValue(group.Key, out var entry))
                    byScorer[group.Key] = entry = (ScorerRef.From(items[0].Score.Scorer), []);
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
