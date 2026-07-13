using Domain;

namespace Application.Analytics;

/// <summary>
/// The read-side identity of a scorer within analytics — the "× Scorer" axis, projected from
/// <see cref="ScorerDescriptor"/>. <see cref="Identity"/> is the stable series key; kind and
/// judge model are carried for display.
/// </summary>
public sealed record ScorerRef(string Identity, ScorerKind Kind, string? JudgeModel)
{
    public static ScorerRef From(ScorerDescriptor descriptor)
        => new(descriptor.Identity, descriptor.Kind, descriptor.JudgeModel);
}

/// <summary>
/// One point on a trend series: a prompt version's aggregate score for a single scorer over a
/// dataset, taken from that version's <b>latest</b> run. <see cref="MeanValue"/> is the mean of
/// the per-fixture <see cref="Score.Value"/>s; <see cref="PassRate"/> is the fraction passed
/// among the scores that carry a pass/fail verdict (null when none do).
/// </summary>
public sealed record TrendPoint(
    Guid PromptVersionId,
    int VersionNumber,
    string? VersionLabel,
    Guid RunId,
    DateTimeOffset RunAt,
    double MeanValue,
    double? PassRate,
    int FixtureCount);

/// <summary>
/// A score series for one scorer across a prompt's versions over a dataset — the
/// <c>Prompt × Dataset × Scorer</c> slice of the score identity, with one <see cref="TrendPoint"/>
/// per version that has a run. Points are ordered by <see cref="TrendPoint.VersionNumber"/> ascending.
/// </summary>
public sealed record TrendSeries(ScorerRef Scorer, IReadOnlyList<TrendPoint> Points);
