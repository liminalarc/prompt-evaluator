using Domain;

namespace Api.EvalRuns;

// ---- Scorer configuration ----

// Weight is optional (2.9): omitted → the domain default (1.0, equal weighting).
public sealed record ConfigureScorerRequest(string Kind, string? Config, string? JudgeModel, double? Weight);

public sealed record ScorerConfigResponse(
    Guid Id, string Kind, string Config, string? JudgeModel, double Weight, string Identity, DateTimeOffset CreatedAt)
{
    public static ScorerConfigResponse From(ScorerConfig config) => new(
        config.Id,
        config.Scorer.Kind.ToString(),
        config.Scorer.Config,
        config.Scorer.JudgeModel,
        config.Weight,
        config.Scorer.Identity,
        config.CreatedAt);
}

// ---- Eval runs ----

public sealed record CreateEvalRunRequest(Guid PromptId, Guid PromptVersionId);

public sealed record ScoreResponse(
    string ScorerKind, string ScorerIdentity, string? JudgeModel, double Value, bool? Passed, string? Detail)
{
    public static ScoreResponse From(Score score) => new(
        score.Scorer.Kind.ToString(),
        score.Scorer.Identity,
        score.Scorer.JudgeModel,
        score.Value,
        score.Passed,
        score.Detail);
}

public sealed record FixtureRunResponse(
    Guid FixtureId,
    string ModelOutput,
    int LatencyMs,
    int InputTokens,
    int OutputTokens,
    decimal? CostUsd,
    IReadOnlyList<ScoreResponse> Scores)
{
    public static FixtureRunResponse From(FixtureRun fixtureRun) => new(
        fixtureRun.FixtureId,
        fixtureRun.ModelOutput,
        fixtureRun.LatencyMs,
        fixtureRun.InputTokens,
        fixtureRun.OutputTokens,
        fixtureRun.CostUsd,
        fixtureRun.Scores.Select(ScoreResponse.From).ToList());
}

public sealed record EvalRunResponse(
    Guid Id,
    Guid PromptId,
    Guid PromptVersionId,
    Guid DatasetId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FixtureRunResponse> Results)
{
    public static EvalRunResponse From(EvalRun run) => new(
        run.Id,
        run.PromptId,
        run.PromptVersionId,
        run.DatasetId,
        run.CreatedAt,
        run.Results.Select(FixtureRunResponse.From).ToList());
}

public sealed record EvalRunSummaryResponse(
    Guid Id, Guid PromptId, Guid PromptVersionId, DateTimeOffset CreatedAt, int FixtureCount, int ScoreCount,
    IReadOnlyList<string> ScorerKinds, double? MeanScore, string? MeanScorerKind)
{
    public static EvalRunSummaryResponse From(EvalRun run)
    {
        var allScores = run.Results.SelectMany(r => r.Scores).ToList();
        // The run's *meaningful* headline score: the LLM-judge mean when the run has one, since
        // deterministic scorers (Regex/exact-match) are near-always 1.0 and inflate a naive overall
        // mean to look perfect (2.19 W23/W30/W33). Falls back to the overall mean when there's no judge.
        var judge = allScores.Where(s => s.Scorer.Kind == ScorerKind.LlmJudge).ToList();
        var chosen = judge.Count > 0 ? judge : allScores;
        double? meanScore = chosen.Count > 0 ? chosen.Average(s => s.Value) : null;
        string? meanScorerKind = chosen.Count == 0 ? null : (judge.Count > 0 ? "LlmJudge" : "overall");

        return new(
            run.Id,
            run.PromptId,
            run.PromptVersionId,
            run.CreatedAt,
            run.Results.Count,
            allScores.Count,
            // Distinct scorer kinds the run scored with, so the runs table reads version · model · scorers (U14).
            allScores.Select(s => s.Scorer.Kind.ToString()).Distinct().OrderBy(k => k).ToList(),
            meanScore,
            meanScorerKind);
    }
}
