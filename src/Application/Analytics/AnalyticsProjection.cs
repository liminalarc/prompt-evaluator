using Domain;

namespace Application.Analytics;

/// <summary>
/// Shared read-side projections over the append-only run history used by trend, regression, and
/// comparison analytics: selecting the latest run per version, and grouping a run's scores by scorer.
/// </summary>
internal static class AnalyticsProjection
{
    /// <summary>One score for one fixture under one scorer, flattened for aggregation.
    /// <see cref="Detail"/> carries the judge rationale (for the rationale-diff, 2.14).</summary>
    public readonly record struct FixtureScore(Guid FixtureId, double Value, bool? Passed, string? Detail);

    /// <summary>
    /// The newest run (by <see cref="EvalRun.CreatedAt"/>) for each version, restricted to versions
    /// that still exist on the prompt. A re-run of a version supersedes its earlier runs.
    /// </summary>
    public static IReadOnlyList<EvalRun> LatestRunPerVersion(
        IEnumerable<EvalRun> runs, IReadOnlySet<Guid> knownVersionIds)
        => runs
            .Where(r => knownVersionIds.Contains(r.PromptVersionId))
            .GroupBy(r => r.PromptVersionId)
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .ToList();

    /// <summary>
    /// Groups a run's per-fixture scores by scorer identity, yielding each scorer's
    /// <see cref="ScorerRef"/> and its flattened <see cref="FixtureScore"/>s.
    /// </summary>
    public static IEnumerable<(ScorerRef Scorer, IReadOnlyList<FixtureScore> Scores)> ByScorer(EvalRun run)
        => run.Results
            .SelectMany(fr => fr.Scores.Select(s => (fr.FixtureId, Score: s)))
            .GroupBy(x => x.Score.Scorer.Identity)
            .Select(g =>
            {
                var items = g.Select(x => new FixtureScore(x.FixtureId, x.Score.Value, x.Score.Passed, x.Score.Detail)).ToList();
                return (ScorerRef.From(g.First().Score.Scorer), (IReadOnlyList<FixtureScore>)items);
            });
}
