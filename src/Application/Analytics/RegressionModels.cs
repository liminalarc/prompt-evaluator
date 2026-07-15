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
/// How much confidence we have that a threshold-clearing drop is a real regression.
/// <see cref="Confirmed"/> — the drop cleared the threshold <b>and</b> a paired t-test found it
/// significant. <see cref="Unverified"/> — the drop cleared the threshold but significance could
/// not be established (fewer than two matched fixtures) or the test was not significant; a
/// <i>possible</i> regression to surface distinctly, not hide.
/// </summary>
public enum RegressionConfidence
{
    Confirmed,
    Unverified,
}

/// <summary>
/// A detected regression: a version whose mean score for a scorer dropped beyond the configured
/// threshold vs. the prior version. When the drop is also statistically significant under a paired
/// t-test over matched per-fixture deltas the flag is <see cref="RegressionConfidence.Confirmed"/>;
/// when significance can't be established (n &lt; 2) or the test isn't significant it is
/// <see cref="RegressionConfidence.Unverified"/> rather than discarded. <see cref="Delta"/> is
/// <c>CurrentMean − PriorMean</c> (negative for a drop); <see cref="PValue"/> is the one-sided
/// p-value for the drop (null when it can't be computed).
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
    int PairedFixtureCount,
    RegressionConfidence Confidence);
