namespace Application.Analytics;

/// <summary>
/// One prompt version's per-fixture scores for a single scorer, drawn from that version's latest
/// run — the paired input to regression detection. <see cref="ScoresByFixture"/> maps fixture id
/// to the normalized score value, so consecutive versions can be matched fixture-for-fixture.
/// </summary>
public sealed record VersionScoreSet(
    Guid VersionId,
    int VersionNumber,
    string? VersionLabel,
    Guid RunId,
    IReadOnlyDictionary<Guid, double> ScoresByFixture);

/// <summary>
/// A detected regression: a version whose mean score for a scorer dropped beyond the configured
/// threshold vs. the prior version, <b>and</b> where the drop is statistically significant under a
/// paired t-test over matched per-fixture deltas. <see cref="Delta"/> is
/// <c>CurrentMean − PriorMean</c> (negative for a drop); <see cref="PValue"/> is the one-sided
/// p-value for the drop.
/// </summary>
public sealed record RegressionFlag(
    ScorerRef Scorer,
    Guid FromVersionId,
    int FromVersionNumber,
    string? FromVersionLabel,
    Guid ToVersionId,
    int ToVersionNumber,
    string? ToVersionLabel,
    double PriorMean,
    double CurrentMean,
    double Delta,
    double? PValue,
    int PairedFixtureCount);
