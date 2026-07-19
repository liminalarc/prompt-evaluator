using Application.Ports;

namespace Application.Analytics;

/// <summary>
/// Derives per-version lifecycle status for a prompt (1.16): which version is <b>Current in source</b>,
/// which are <b>Backport-eligible</b> (score higher than Current and worth shipping), and which
/// <b>Regressed</b>. Every comparison keys off <see cref="ScorerRef.Identity"/> over a shared dataset —
/// runs made under a different scorer config are a different yardstick and are never blended (the 5.1
/// F1 / 2026-07-18 decision). Read-only over the append-only run history.
///
/// Interim eligibility rule (weighted/scorer-priority eligibility is deferred to 2.9): a non-Current
/// version is eligible when, across every shared same-scorer-config series where both it and Current
/// have a latest-run mean, it is ≥ Current on all and strictly higher on at least one.
/// </summary>
public sealed class VersionStatusHandler(
    IPromptRepository prompts,
    IEvalRunRepository runs,
    IDatasetRepository datasets,
    RegressionDetector detector)
{
    private const double Epsilon = 1e-9;

    /// <summary>Null when the prompt does not exist (Api → 404).</summary>
    public async Task<PromptVersionStatus?> HandleAsync(Guid promptId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versions = prompt.Versions; // ordered by version number
        var knownVersionIds = versions.Select(v => v.Id).ToHashSet();
        var currentId = prompt.CurrentVersionId;

        // Each element is one (dataset × scorer-identity) series: versionId → its latest-run mean.
        var seriesMeans = new List<Dictionary<Guid, double>>();
        // Versions flagged by the 1.11 regression detector (over same-scorer-config consecutive versions).
        var regressed = new HashSet<Guid>();

        foreach (var dataset in await datasets.ListByPromptAsync(promptId, ct))
        {
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

            foreach (var vm in meansByScorer.Values)
                seriesMeans.Add(vm);

            foreach (var (scorer, sets) in setsByScorer.Values)
                foreach (var flag in detector.Detect(scorer, sets.OrderBy(s => s.VersionNumber).ToList()))
                    regressed.Add(flag.ToVersionId);
        }

        var statuses = versions
            .Select(v => new VersionStatus(
                v.Id,
                v.VersionNumber,
                v.Label,
                IsCurrent: currentId == v.Id,
                BackportEligible: IsBackportEligible(v.Id, currentId, seriesMeans),
                Regressed: regressed.Contains(v.Id)))
            .ToList();

        return new PromptVersionStatus(promptId, currentId, statuses);
    }

    // ≥ Current on every shared series, strictly higher on ≥ 1 — over at least one shared series.
    private static bool IsBackportEligible(
        Guid versionId, Guid? currentId, IReadOnlyList<Dictionary<Guid, double>> seriesMeans)
    {
        if (currentId is not { } cId || versionId == cId)
            return false;

        var shared = 0;
        var strictlyHigher = false;
        foreach (var series in seriesMeans)
        {
            if (!series.TryGetValue(cId, out var currentMean) || !series.TryGetValue(versionId, out var versionMean))
                continue;
            shared++;
            if (versionMean < currentMean - Epsilon)
                return false; // regressed on this scorer vs Current → not a clean backport
            if (versionMean > currentMean + Epsilon)
                strictlyHigher = true;
        }

        return shared > 0 && strictlyHigher;
    }
}
