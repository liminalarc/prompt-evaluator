using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Builds the concrete <see cref="IScorer"/> for a <see cref="ScorerDescriptor"/>. Deterministic
/// kinds are constructed in-process; the LLM-judge scorer delegates to the eval-runner via the
/// injected <see cref="IEvaluationRunner"/>. When no runner is supplied, LLM-judge scorers cannot
/// be built (deterministic-only construction remains valid).
/// </summary>
public sealed class ScorerFactory(IEvaluationRunner? runner = null)
{
    public IScorer Create(ScorerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Kind switch
        {
            ScorerKind.Regex => new RegexScorer(descriptor),
            ScorerKind.JsonSchema => new JsonSchemaScorer(descriptor),
            ScorerKind.ExactMatch => new ExactMatchScorer(descriptor),
            ScorerKind.FuzzyMatch => new FuzzyMatchScorer(descriptor),
            ScorerKind.Latency => new LatencyScorer(descriptor),
            ScorerKind.Cost => new CostScorer(descriptor),
            ScorerKind.LlmJudge => runner is not null
                ? new LlmJudgeScorer(descriptor, runner)
                : throw new NotSupportedException("LLM-judge scoring requires an IEvaluationRunner."),
            _ => throw new NotSupportedException($"Unknown scorer kind '{descriptor.Kind}'."),
        };
    }
}
