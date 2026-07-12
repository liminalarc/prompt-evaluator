using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Scores an output by delegating to the eval-runner's LLM judge. The descriptor's config is the
/// rubric and its judge model is passed explicitly (both are part of the scorer's identity). The
/// verdict is structured — this scorer never parses prose.
/// </summary>
public sealed class LlmJudgeScorer(ScorerDescriptor descriptor, IEvaluationRunner runner) : IScorer
{
    public ScorerDescriptor Descriptor { get; } = descriptor;

    public async Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        var verdict = await runner.JudgeAsync(
            rubric: Descriptor.Config,
            input: context.Input,
            output: context.ModelOutput,
            expected: context.ExpectedOutput,
            judgeModel: Descriptor.JudgeModel!,
            ct);

        return new ScoreOutcome(verdict.Score, verdict.Passed, verdict.Rationale);
    }
}
