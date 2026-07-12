using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Builds the concrete <see cref="IScorer"/> for a <see cref="ScorerDescriptor"/>. Deterministic
/// kinds are constructed in-process here; the LLM-judge scorer is wired in slice 4 once the
/// eval-runner seam (<c>IEvaluationRunner.JudgeAsync</c>) exists.
/// </summary>
public sealed class ScorerFactory
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
            ScorerKind.LlmJudge => throw new NotSupportedException(
                "LLM-judge scoring is wired in slice 4 (needs IEvaluationRunner)."),
            _ => throw new NotSupportedException($"Unknown scorer kind '{descriptor.Kind}'."),
        };
    }
}
