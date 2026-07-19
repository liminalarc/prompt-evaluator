using Application.Ports;
using Domain;

namespace Application.Analytics;

/// <summary>
/// Builds score-stability (variance) series for a prompt over a dataset, one series per scorer.
/// Unlike <see cref="TrendAnalyticsHandler"/> (which takes each version's <b>latest</b> run), this
/// aggregates <b>every</b> run of a version so a run-to-run wobble reads as spread, not signal —
/// the "one run lies" lesson from 5.1 (R4 / spec 2.14). Read-only over the append-only run history.
/// </summary>
public sealed class VarianceAnalyticsHandler(IEvalRunRepository runs, IPromptRepository prompts)
{
    /// <summary>Null when the prompt doesn't exist (Api → 404); empty list when it has no runs.</summary>
    public async Task<IReadOnlyList<ScorerVariance>?> HandleAsync(
        Guid promptId, Guid datasetId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versions = prompt.Versions.ToDictionary(v => v.Id);
        var allRuns = await runs.ListByPromptAndDatasetAsync(promptId, datasetId, ct);

        // scorer identity -> (ScorerRef, version id -> that version's runs, each as a per-fixture map).
        var byScorer = new Dictionary<string, (ScorerRef Scorer, Dictionary<Guid, List<Dictionary<Guid, double>>> Runs)>();

        foreach (var run in allRuns)
        {
            if (!versions.ContainsKey(run.PromptVersionId))
                continue;

            foreach (var (scorer, scores) in AnalyticsProjection.ByScorer(run))
            {
                if (!byScorer.TryGetValue(scorer.Identity, out var entry))
                    byScorer[scorer.Identity] = entry = (scorer, new Dictionary<Guid, List<Dictionary<Guid, double>>>());
                if (!entry.Runs.TryGetValue(run.PromptVersionId, out var versionRuns))
                    entry.Runs[run.PromptVersionId] = versionRuns = [];

                // One score per fixture per scorer per run; average defensively if a fixture repeats.
                versionRuns.Add(scores
                    .GroupBy(s => s.FixtureId)
                    .ToDictionary(g => g.Key, g => g.Average(s => s.Value)));
            }
        }

        return byScorer.Values
            .Select(e => new ScorerVariance(e.Scorer, BuildVersions(e.Runs, versions)))
            .OrderBy(s => s.Scorer.Kind)
            .ThenBy(s => s.Scorer.Identity, StringComparer.Ordinal)
            .ToList();
    }

    private static List<VersionVariance> BuildVersions(
        Dictionary<Guid, List<Dictionary<Guid, double>>> runsByVersion,
        IReadOnlyDictionary<Guid, PromptVersion> versions)
        => runsByVersion
            .Select(kv =>
            {
                var version = versions[kv.Key];
                var versionRuns = kv.Value;

                // Aggregate: each run's mean over its fixtures → spread across runs.
                var runMeans = versionRuns.Select(r => r.Values.Average()).ToList();

                // Per fixture: its value in each run that scored it → spread across runs.
                var fixtures = versionRuns
                    .SelectMany(r => r.Keys)
                    .Distinct()
                    .Select(fixtureId =>
                    {
                        var vals = versionRuns
                            .Where(r => r.ContainsKey(fixtureId))
                            .Select(r => r[fixtureId])
                            .ToList();
                        return new FixtureVariance(fixtureId, VarianceStat.From(vals));
                    })
                    .OrderBy(f => f.FixtureId)
                    .ToList();

                return new VersionVariance(
                    version.Id, version.VersionNumber, version.Label,
                    RunCount: versionRuns.Count,
                    Aggregate: VarianceStat.From(runMeans),
                    Fixtures: fixtures);
            })
            .OrderBy(v => v.VersionNumber)
            .ToList();
}
