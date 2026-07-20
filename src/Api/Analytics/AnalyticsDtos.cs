using Application.Analytics;

namespace Api.Analytics;

// ---- Trends ----

public sealed record ScorerRefResponse(string Identity, string Kind, string? JudgeModel)
{
    public static ScorerRefResponse From(ScorerRef s) => new(s.Identity, s.Kind.ToString(), s.JudgeModel);
}

public sealed record TrendPointResponse(
    Guid PromptVersionId, int VersionNumber, string? VersionLabel, Guid RunId,
    DateTimeOffset RunAt, double MeanValue, double? PassRate, int FixtureCount)
{
    public static TrendPointResponse From(TrendPoint p) => new(
        p.PromptVersionId, p.VersionNumber, p.VersionLabel, p.RunId, p.RunAt, p.MeanValue, p.PassRate, p.FixtureCount);
}

public sealed record TrendSeriesResponse(ScorerRefResponse Scorer, IReadOnlyList<TrendPointResponse> Points)
{
    public static TrendSeriesResponse From(TrendSeries s) => new(
        ScorerRefResponse.From(s.Scorer), s.Points.Select(TrendPointResponse.From).ToList());
}

// ---- Weighted composite (2.9) ----

public sealed record CompositeTrendPointResponse(
    Guid PromptVersionId, int VersionNumber, string? VersionLabel, Guid RunId,
    DateTimeOffset RunAt, double CompositeValue, int ScorerCount)
{
    public static CompositeTrendPointResponse From(CompositeTrendPoint p) => new(
        p.PromptVersionId, p.VersionNumber, p.VersionLabel, p.RunId, p.RunAt, p.CompositeValue, p.ScorerCount);
}

// ---- Regressions ----

public sealed record RegressionFlagResponse(
    ScorerRefResponse Scorer,
    Guid FromVersionId, int FromVersionNumber, string? FromVersionLabel,
    Guid ToVersionId, int ToVersionNumber, string? ToVersionLabel,
    double PriorMean, double CurrentMean, double Delta, double? PValue, int PairedFixtureCount,
    string Confidence)
{
    public static RegressionFlagResponse From(RegressionFlag f) => new(
        ScorerRefResponse.From(f.Scorer),
        f.FromVersionId, f.FromVersionNumber, f.FromVersionLabel,
        f.ToVersionId, f.ToVersionNumber, f.ToVersionLabel,
        f.PriorMean, f.CurrentMean, f.Delta, f.PValue, f.PairedFixtureCount,
        f.Confidence.ToString());
}

// ---- Comparison ----

public sealed record FixtureDeltaResponse(
    Guid FixtureId, string? FixtureLabel, double? FromValue, double? ToValue, double? Delta,
    string? FromRationale, string? ToRationale)
{
    public static FixtureDeltaResponse From(FixtureDelta d) =>
        new(d.FixtureId, d.FixtureLabel, d.FromValue, d.ToValue, d.Delta, d.FromRationale, d.ToRationale);
}

public sealed record ScorerComparisonResponse(
    ScorerRefResponse Scorer, double? FromMean, double? ToMean, double? Delta,
    IReadOnlyList<FixtureDeltaResponse> Fixtures)
{
    public static ScorerComparisonResponse From(ScorerComparison c) => new(
        ScorerRefResponse.From(c.Scorer), c.FromMean, c.ToMean, c.Delta,
        c.Fixtures.Select(FixtureDeltaResponse.From).ToList());
}

public sealed record VersionComparisonResponse(
    Guid FromVersionId, int FromVersionNumber, string? FromVersionLabel, Guid? FromRunId,
    Guid ToVersionId, int ToVersionNumber, string? ToVersionLabel, Guid? ToRunId,
    IReadOnlyList<ScorerComparisonResponse> Scorers)
{
    public static VersionComparisonResponse From(VersionComparison c) => new(
        c.FromVersionId, c.FromVersionNumber, c.FromVersionLabel, c.FromRunId,
        c.ToVersionId, c.ToVersionNumber, c.ToVersionLabel, c.ToRunId,
        c.Scorers.Select(ScorerComparisonResponse.From).ToList());
}

// ---- Variance (score stability over repeated runs — 2.14) ----

public sealed record VarianceStatResponse(double Mean, double StdDev, int SampleCount, double Min, double Max)
{
    public static VarianceStatResponse From(VarianceStat s) =>
        new(s.Mean, s.StdDev, s.SampleCount, s.Min, s.Max);
}

public sealed record FixtureVarianceResponse(Guid FixtureId, VarianceStatResponse Value)
{
    public static FixtureVarianceResponse From(FixtureVariance f) =>
        new(f.FixtureId, VarianceStatResponse.From(f.Value));
}

public sealed record VersionVarianceResponse(
    Guid PromptVersionId, int VersionNumber, string? VersionLabel, int RunCount,
    VarianceStatResponse Aggregate, IReadOnlyList<FixtureVarianceResponse> Fixtures)
{
    public static VersionVarianceResponse From(VersionVariance v) => new(
        v.PromptVersionId, v.VersionNumber, v.VersionLabel, v.RunCount,
        VarianceStatResponse.From(v.Aggregate), v.Fixtures.Select(FixtureVarianceResponse.From).ToList());
}

public sealed record ScorerVarianceResponse(ScorerRefResponse Scorer, IReadOnlyList<VersionVarianceResponse> Versions)
{
    public static ScorerVarianceResponse From(ScorerVariance s) => new(
        ScorerRefResponse.From(s.Scorer), s.Versions.Select(VersionVarianceResponse.From).ToList());
}
