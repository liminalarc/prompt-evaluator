using Application.Ports;

namespace Application.Analytics;

/// <summary>
/// Builds the weighted-composite trend series (2.9) for a prompt over a dataset: one point per
/// version, each the weighted mean of that version's <b>latest</b> run's per-scorer means. Weights
/// come from the dataset's current <see cref="Domain.ScorerConfig"/> set; a run scorer with no
/// current config row falls back to the default weight (see <see cref="CompositeScoring"/>). This is
/// the headline "overall quality" number that sits alongside the per-scorer series
/// (<see cref="TrendAnalyticsHandler"/>). Read-only over the append-only run history.
/// </summary>
public sealed class CompositeTrendHandler(
    IEvalRunRepository runs, IPromptRepository prompts, IScorerConfigRepository scorerConfigs)
{
    /// <summary>Returns null when the prompt does not exist (Api → 404); empty when it has no scored runs.</summary>
    public async Task<IReadOnlyList<CompositeTrendPoint>?> HandleAsync(
        Guid promptId, Guid datasetId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versions = prompt.Versions.ToDictionary(v => v.Id);
        var weights = await WeightsByIdentityAsync(datasetId, ct);

        var allRuns = await runs.ListByPromptAndDatasetAsync(promptId, datasetId, ct);
        var latestPerVersion = AnalyticsProjection.LatestRunPerVersion(allRuns, versions.Keys.ToHashSet());

        var points = new List<CompositeTrendPoint>();
        foreach (var run in latestPerVersion)
        {
            var meansByScorer = new Dictionary<string, double>();
            foreach (var (scorer, scores) in AnalyticsProjection.ByScorer(run))
                meansByScorer[scorer.Identity] = scores.Average(s => s.Value);

            if (CompositeScoring.WeightedComposite(meansByScorer, weights) is not { } composite)
                continue; // a run with no scores contributes no composite point

            var version = versions[run.PromptVersionId];
            points.Add(new CompositeTrendPoint(
                version.Id, version.VersionNumber, version.Label,
                run.Id, run.CreatedAt, composite, meansByScorer.Count));
        }

        return points.OrderBy(p => p.VersionNumber).ToList();
    }

    // Current per-scorer weights for the dataset, keyed by scorer identity. Defensive against a
    // dataset that somehow holds two configs with the same identity (last write wins).
    private async Task<IReadOnlyDictionary<string, double>> WeightsByIdentityAsync(
        Guid datasetId, CancellationToken ct)
    {
        var weights = new Dictionary<string, double>();
        foreach (var config in await scorerConfigs.ListByDatasetAsync(datasetId, ct))
            weights[config.Scorer.Identity] = config.Weight;
        return weights;
    }
}
