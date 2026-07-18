namespace Application.Analytics;

/// <summary>
/// One fixture's score on each side of a version comparison, for a single scorer.
/// <see cref="FromValue"/>/<see cref="ToValue"/> are null when the fixture wasn't scored on that
/// side (e.g. added to the dataset between runs); <see cref="Delta"/> (<c>To − From</c>) is null
/// unless both sides are present.
/// </summary>
public sealed record FixtureDelta(
    Guid FixtureId, string? FixtureLabel, double? FromValue, double? ToValue, double? Delta);

/// <summary>
/// A single scorer's view of a version comparison: the aggregate means on each side (over that
/// side's own fixtures), their <see cref="Delta"/>, and the per-fixture breakdown.
/// </summary>
public sealed record ScorerComparison(
    ScorerRef Scorer,
    double? FromMean,
    double? ToMean,
    double? Delta,
    IReadOnlyList<FixtureDelta> Fixtures);

/// <summary>
/// A version-vs-version comparison over a dataset: for every scorer present on either side, the
/// aggregate and per-fixture deltas between the two versions' latest runs.
/// </summary>
public sealed record VersionComparison(
    Guid FromVersionId,
    int FromVersionNumber,
    string? FromVersionLabel,
    Guid? FromRunId,
    Guid ToVersionId,
    int ToVersionNumber,
    string? ToVersionLabel,
    Guid? ToRunId,
    IReadOnlyList<ScorerComparison> Scorers);
