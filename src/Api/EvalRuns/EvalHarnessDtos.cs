using Domain;

namespace Api.EvalRuns;

// ---- Scorer configuration ----

public sealed record ConfigureScorerRequest(string Kind, string? Config, string? JudgeModel);

public sealed record ScorerConfigResponse(
    Guid Id, string Kind, string Config, string? JudgeModel, string Identity, DateTimeOffset CreatedAt)
{
    public static ScorerConfigResponse From(ScorerConfig config) => new(
        config.Id,
        config.Scorer.Kind.ToString(),
        config.Scorer.Config,
        config.Scorer.JudgeModel,
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
    Guid Id, Guid PromptId, Guid PromptVersionId, DateTimeOffset CreatedAt, int FixtureCount, int ScoreCount)
{
    public static EvalRunSummaryResponse From(EvalRun run) => new(
        run.Id,
        run.PromptId,
        run.PromptVersionId,
        run.CreatedAt,
        run.Results.Count,
        run.Results.Sum(r => r.Scores.Count));
}
