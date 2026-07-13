using Application.Ports;
using Domain;

namespace Application.Analytics;

/// <summary>
/// Builds score trend series for a prompt over a dataset, one series per scorer
/// (<c>Prompt × Dataset × Scorer</c>). Each version contributes a single point taken from its
/// <b>latest</b> run (newest <see cref="EvalRun.CreatedAt"/>), so a re-run supersedes an earlier
/// one. Points are ordered by version number so the series reads left-to-right as the prompt evolved.
/// Read-only over the append-only run history — no domain mutation.
/// </summary>
public sealed class TrendAnalyticsHandler(IEvalRunRepository runs, IPromptRepository prompts)
{
    /// <summary>Returns null when the prompt does not exist (Api → 404); empty list when it has no runs.</summary>
    public async Task<IReadOnlyList<TrendSeries>?> HandleAsync(
        Guid promptId, Guid datasetId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versions = prompt.Versions.ToDictionary(v => v.Id);
        var allRuns = await runs.ListByPromptAndDatasetAsync(promptId, datasetId, ct);
        var latestPerVersion = AnalyticsProjection.LatestRunPerVersion(allRuns, versions.Keys.ToHashSet());

        // (scorer identity) -> its descriptor + the points it accumulates across versions.
        var byScorer = new Dictionary<string, (ScorerRef Scorer, List<TrendPoint> Points)>();

        foreach (var run in latestPerVersion)
        {
            var version = versions[run.PromptVersionId];

            foreach (var (scorer, scores) in AnalyticsProjection.ByScorer(run))
            {
                var verdicts = scores.Where(s => s.Passed.HasValue).ToList();

                var point = new TrendPoint(
                    PromptVersionId: version.Id,
                    VersionNumber: version.VersionNumber,
                    VersionLabel: version.Label,
                    RunId: run.Id,
                    RunAt: run.CreatedAt,
                    MeanValue: scores.Average(s => s.Value),
                    PassRate: verdicts.Count == 0 ? null : verdicts.Count(s => s.Passed!.Value) / (double)verdicts.Count,
                    FixtureCount: scores.Count);

                if (!byScorer.TryGetValue(scorer.Identity, out var entry))
                    byScorer[scorer.Identity] = entry = (scorer, []);
                entry.Points.Add(point);
            }
        }

        return byScorer.Values
            .Select(e => new TrendSeries(e.Scorer, e.Points.OrderBy(p => p.VersionNumber).ToList()))
            .OrderBy(s => s.Scorer.Kind)
            .ThenBy(s => s.Scorer.Identity, StringComparer.Ordinal)
            .ToList();
    }
}
