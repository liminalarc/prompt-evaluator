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

public sealed record FixtureDeltaResponse(Guid FixtureId, double? FromValue, double? ToValue, double? Delta)
{
    public static FixtureDeltaResponse From(FixtureDelta d) => new(d.FixtureId, d.FromValue, d.ToValue, d.Delta);
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
