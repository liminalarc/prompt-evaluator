using Application.Ports;
using Domain;

namespace Application.Analytics;

/// <summary>
/// Compares two prompt versions over a dataset, per scorer: aggregate means on each side plus a
/// per-fixture delta breakdown (matched by fixture id), using each version's latest run. Read-only.
/// </summary>
public sealed class ComparisonAnalyticsHandler(IEvalRunRepository runs, IPromptRepository prompts)
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

        var allRuns = await runs.ListByPromptAndDatasetAsync(promptId, datasetId, ct);
        var latest = AnalyticsProjection.LatestRunPerVersion(allRuns, prompt.Versions.Select(v => v.Id).ToHashSet());

        var fromRun = latest.FirstOrDefault(r => r.PromptVersionId == fromVersionId);
        var toRun = latest.FirstOrDefault(r => r.PromptVersionId == toVersionId);

        // scorer identity -> (ScorerRef, from fixture→value, to fixture→value)
        var byScorer = new Dictionary<string, (ScorerRef Scorer, Dictionary<Guid, double> From, Dictionary<Guid, double> To)>();

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
                    target[s.FixtureId] = s.Value;
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
                    double? from = e.From.TryGetValue(id, out var fv) ? fv : null;
                    double? to = e.To.TryGetValue(id, out var tv) ? tv : null;
                    return new FixtureDelta(id, from, to, from.HasValue && to.HasValue ? to - from : null);
                }).ToList();

                double? fromMean = e.From.Count > 0 ? e.From.Values.Average() : null;
                double? toMean = e.To.Count > 0 ? e.To.Values.Average() : null;
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
