using Application.Ports;
using Domain;

namespace Application.Analytics;

/// <summary>
/// Compares two prompt versions over a dataset, per scorer: aggregate means on each side plus a
/// per-fixture delta breakdown (matched by fixture id), using each version's latest run. Read-only.
/// </summary>
public sealed class ComparisonAnalyticsHandler(
    IEvalRunRepository runs, IPromptRepository prompts, IDatasetRepository datasets)
{
    /// <summary>Returns null when the prompt or either version does not exist (Api → 404).</summary>
    public async Task<VersionComparison?> HandleAsync(
        Guid promptId, Guid datasetId, Guid fromVersionId, Guid toVersionId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        var fromVersion = prompt?.Versions.FirstOrDefault(v => v.Id == fromVersionId);
        var toVersion = prompt?.Versions.FirstOrDefault(v => v.Id == toVersionId);
        if (prompt is null || fromVersion is null || toVersion is null)
            return null;

        // Fixture labels (U7) so the per-fixture table reads by scenario, not opaque GUID.
        var dataset = await datasets.GetByIdAsync(datasetId, ct);
        var labels = dataset?.Fixtures.ToDictionary(f => f.Id, f => f.Label) ?? [];

        var allRuns = await runs.ListByPromptAndDatasetAsync(promptId, datasetId, ct);
        var latest = AnalyticsProjection.LatestRunPerVersion(allRuns, prompt.Versions.Select(v => v.Id).ToHashSet());

        var fromRun = latest.FirstOrDefault(r => r.PromptVersionId == fromVersionId);
        var toRun = latest.FirstOrDefault(r => r.PromptVersionId == toVersionId);

        // scorer identity -> (ScorerRef, from fixture→score, to fixture→score). We keep the whole
        // FixtureScore (not just the value) so the per-fixture judge rationale is available for the
        // rationale-diff (2.14): a real quality change can score flat, and only the reasoning shows it.
        var byScorer = new Dictionary<string, (ScorerRef Scorer,
            Dictionary<Guid, AnalyticsProjection.FixtureScore> From,
            Dictionary<Guid, AnalyticsProjection.FixtureScore> To)>();

        void Collect(EvalRun? run, bool isFrom)
        {
            if (run is null)
                return;
            foreach (var (scorer, scores) in AnalyticsProjection.ByScorer(run))
            {
                if (!byScorer.TryGetValue(scorer.Identity, out var entry))
                    byScorer[scorer.Identity] = entry = (scorer, [], []);
                var target = isFrom ? entry.From : entry.To;
                foreach (var s in scores)
                    target[s.FixtureId] = s;
            }
        }

        Collect(fromRun, isFrom: true);
        Collect(toRun, isFrom: false);

        var scorerComparisons = byScorer.Values
            .Select(e =>
            {
                var fixtureIds = e.From.Keys.Union(e.To.Keys).OrderBy(id => id).ToList();
                var fixtures = fixtureIds.Select(id =>
                {
                    var hasFrom = e.From.TryGetValue(id, out var fs);
                    var hasTo = e.To.TryGetValue(id, out var ts);
                    double? from = hasFrom ? fs.Value : null;
                    double? to = hasTo ? ts.Value : null;
                    var label = labels.TryGetValue(id, out var l) ? l : null;
                    return new FixtureDelta(
                        id, label, from, to, from.HasValue && to.HasValue ? to - from : null,
                        FromRationale: hasFrom ? fs.Detail : null,
                        ToRationale: hasTo ? ts.Detail : null);
                }).ToList();

                double? fromMean = e.From.Count > 0 ? e.From.Values.Average(s => s.Value) : null;
                double? toMean = e.To.Count > 0 ? e.To.Values.Average(s => s.Value) : null;
                double? delta = fromMean.HasValue && toMean.HasValue ? toMean - fromMean : null;

                return new ScorerComparison(e.Scorer, fromMean, toMean, delta, fixtures);
            })
            .OrderBy(s => s.Scorer.Kind)
            .ThenBy(s => s.Scorer.Identity, StringComparer.Ordinal)
            .ToList();

        return new VersionComparison(
            fromVersion.Id, fromVersion.VersionNumber, fromVersion.Label, fromRun?.Id,
            toVersion.Id, toVersion.VersionNumber, toVersion.Label, toRun?.Id,
            scorerComparisons);
    }
}
