using Application.Ports;

namespace Application.Analytics;

/// <summary>
/// Derives per-version lifecycle status for a prompt (1.16): which version is <b>Current in source</b>,
/// which are <b>Backport-eligible</b> (a real improvement over Current), the single <b>backport target</b>
/// (the one to actually ship), and which <b>Regressed</b>. Every comparison keys off
/// <see cref="ScorerRef.Identity"/> over a shared dataset — runs made under a different scorer config are
/// a different yardstick and are never blended (the 5.1 F1 / 2026-07-18 decision). Read-only over the
/// append-only run history.
///
/// Eligibility &amp; the single-target pick are keyed off the <b>normalized weighted composite vs Current</b>
/// (2.9), replacing 1.16's interim unweighted rule. A version is <b>eligible</b> when it does not regress
/// on any same-scorer-config series it shares with Current (the safety floor) <b>and</b> its weighted
/// composite improvement over Current is strictly positive. The <b>target</b> is the eligible version with
/// the greatest weighted improvement (ties → later version). The improvement is
/// <c>Σ_shared w·(cand − cur) / Σ_{Current's series} w</c> — weights from each dataset's
/// <see cref="Domain.ScorerConfig"/> — so a high-signal scorer (LLM judge) outweighs a low-signal one
/// (RegEx), and normalizing by Current's <em>total</em> weight means a candidate that shares only a
/// low-weight scorer with Current cannot out-rank one that improves on Current's high-weight yardstick.
/// This resolves the scorer-config-change confound (round-debrief): the raw-mean rule could pick an old
/// version scored high on an old rubric; the weighted composite picks the maintainer-iterated one.
/// </summary>
public sealed class VersionStatusHandler(
    IPromptRepository prompts,
    IEvalRunRepository runs,
    IDatasetRepository datasets,
    IScorerConfigRepository scorerConfigs,
    RegressionDetector detector)
{
    private const double Epsilon = 1e-9;

    // One (dataset × scorer-identity) series: its composite weight + versionId → latest-run mean.
    private readonly record struct WeightedSeries(double Weight, Dictionary<Guid, double> Means);

    /// <summary>Null when the prompt does not exist (Api → 404).</summary>
    public async Task<PromptVersionStatus?> HandleAsync(Guid promptId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versions = prompt.Versions; // ordered by version number
        var knownVersionIds = versions.Select(v => v.Id).ToHashSet();
        var currentId = prompt.CurrentVersionId;

        // Each element is one (dataset × scorer-identity) series: its composite weight + the
        // versionId → latest-run mean map.
        var seriesList = new List<WeightedSeries>();
        // Versions flagged by the 1.11 regression detector (over same-scorer-config consecutive versions).
        var regressed = new HashSet<Guid>();

        foreach (var dataset in await datasets.ListByPromptAsync(promptId, ct))
        {
            var weights = await WeightsByIdentityAsync(dataset.Id, ct);
            var latestPerVersion = AnalyticsProjection.LatestRunPerVersion(
                await runs.ListByPromptAndDatasetAsync(promptId, dataset.Id, ct), knownVersionIds);

            var meansByScorer = new Dictionary<string, Dictionary<Guid, double>>();
            var setsByScorer = new Dictionary<string, (ScorerRef Scorer, List<VersionScoreSet> Sets)>();

            foreach (var run in latestPerVersion)
            {
                var version = versions.First(v => v.Id == run.PromptVersionId);
                foreach (var (scorer, scores) in AnalyticsProjection.ByScorer(run))
                {
                    var byFixture = new Dictionary<Guid, double>();
                    foreach (var s in scores)
                        byFixture[s.FixtureId] = s.Value; // one score per fixture per scorer

                    var mean = byFixture.Count == 0 ? 0.0 : byFixture.Values.Average();

                    if (!meansByScorer.TryGetValue(scorer.Identity, out var vm))
                        meansByScorer[scorer.Identity] = vm = new Dictionary<Guid, double>();
                    vm[version.Id] = mean;

                    if (!setsByScorer.TryGetValue(scorer.Identity, out var entry))
                        setsByScorer[scorer.Identity] = entry = (scorer, []);
                    entry.Sets.Add(new VersionScoreSet(
                        version.Id, version.VersionNumber, version.Label, run.Id, byFixture));
                }
            }

            foreach (var (identity, vm) in meansByScorer)
            {
                var weight = weights.GetValueOrDefault(identity, CompositeScoring.DefaultWeight);
                seriesList.Add(new WeightedSeries(weight, vm));
            }

            foreach (var (scorer, sets) in setsByScorer.Values)
                foreach (var flag in detector.Detect(scorer, sets.OrderBy(s => s.VersionNumber).ToList()))
                    regressed.Add(flag.ToVersionId);
        }

        // R9 (2.9a): hold the subject model constant. Only versions sharing Current's model can be
        // eligible / the target — a version scored on a different (e.g. stronger) model would confound
        // the prompt's effect with the model's (the R5 confound, resurfacing in the backport recommender).
        // Cross-model versions are excluded from the comparison and counted for the UI warning.
        var currentModel = currentId is { } curId
            ? versions.FirstOrDefault(v => v.Id == curId)?.TargetModel
            : null;

        bool SharesCurrentModel(Domain.PromptVersion v) => currentModel is null || v.TargetModel == currentModel;

        var crossModelVersionsExcluded = currentModel is null
            ? 0
            : versions.Count(v => v.Id != currentId && v.TargetModel != currentModel);

        var eligibleIds = versions
            .Where(v => SharesCurrentModel(v) && IsBackportEligible(v.Id, currentId, seriesList))
            .Select(v => v.Id)
            .ToHashSet();

        // Exactly one recommended target: among the eligible versions (all beat Current), the one with
        // the greatest normalized weighted-composite improvement over Current — so a high-signal scorer
        // outweighs a low-signal one and a version that only shares a low-weight scorer with Current
        // cannot out-rank one that improves on Current's high-weight yardstick (2.9). Ties → later version.
        var targetId = versions
            .Where(v => eligibleIds.Contains(v.Id))
            .OrderByDescending(v => WeightedDeltaVsCurrent(v.Id, currentId, seriesList) ?? double.NegativeInfinity)
            .ThenByDescending(v => v.VersionNumber)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefault();

        var statuses = versions
            .Select(v => new VersionStatus(
                v.Id,
                v.VersionNumber,
                v.Label,
                IsCurrent: currentId == v.Id,
                BackportEligible: eligibleIds.Contains(v.Id),
                IsBackportTarget: targetId == v.Id,
                Regressed: regressed.Contains(v.Id)))
            .ToList();

        return new PromptVersionStatus(promptId, currentId, targetId, statuses, crossModelVersionsExcluded);
    }

    // Current per-scorer weights for a dataset, keyed by scorer identity (2.9). Last write wins if a
    // dataset somehow holds two configs with the same identity.
    private async Task<IReadOnlyDictionary<string, double>> WeightsByIdentityAsync(
        Guid datasetId, CancellationToken ct)
    {
        var weights = new Dictionary<string, double>();
        foreach (var config in await scorerConfigs.ListByDatasetAsync(datasetId, ct))
            weights[config.Scorer.Identity] = config.Weight;
        return weights;
    }

    // The normalized weighted-composite improvement of a version over Current: the weight-blended sum
    // of per-series deltas over the series shared with Current, divided by the total weight of ALL of
    // Current's series. Normalizing by Current's full weight means a candidate missing one of Current's
    // (heavily-weighted) scorers is diluted rather than flattered by the one easy series it shares.
    // Null when Current has no series to compare against (or no Current set).
    private static double? WeightedDeltaVsCurrent(
        Guid versionId, Guid? currentId, IReadOnlyList<WeightedSeries> seriesList)
    {
        if (currentId is not { } cId)
            return null;

        var numerator = 0.0;
        var currentWeight = 0.0;
        foreach (var series in seriesList)
        {
            if (!series.Means.TryGetValue(cId, out var currentMean))
                continue; // only Current's own yardstick contributes to the normalization
            currentWeight += series.Weight;
            if (series.Means.TryGetValue(versionId, out var versionMean))
                numerator += series.Weight * (versionMean - currentMean);
        }

        return currentWeight <= 0.0 ? null : numerator / currentWeight;
    }

    // Eligible when: no regression on any same-scorer-config series shared with Current (the safety
    // floor, retained from 1.16) AND a strictly-positive weighted-composite improvement over Current.
    private static bool IsBackportEligible(
        Guid versionId, Guid? currentId, IReadOnlyList<WeightedSeries> seriesList)
    {
        if (currentId is not { } cId || versionId == cId)
            return false;

        var shared = 0;
        foreach (var series in seriesList)
        {
            if (!series.Means.TryGetValue(cId, out var currentMean) ||
                !series.Means.TryGetValue(versionId, out var versionMean))
                continue;
            shared++;
            if (versionMean < currentMean - Epsilon)
                return false; // regressed on this scorer vs Current → not a clean backport
        }

        if (shared == 0)
            return false;

        return WeightedDeltaVsCurrent(versionId, cId, seriesList) is { } delta && delta > Epsilon;
    }
}
