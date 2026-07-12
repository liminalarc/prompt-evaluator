using Domain;

namespace Application.Ports;

/// <summary>
/// Everything a scorer needs to judge one fixture's execution: the fixture's input, its optional
/// expected/reference output, the subject model's actual output, and the latency/cost of producing it.
/// </summary>
public sealed record ScoringContext(
    string Input,
    string? ExpectedOutput,
    string ModelOutput,
    int LatencyMs,
    decimal? CostUsd);

/// <summary>A scorer's verdict: a normalized <see cref="Value"/> in [0, 1], an optional pass/fail, and optional detail.</summary>
public sealed record ScoreOutcome(double Value, bool? Passed, string? Detail);

/// <summary>
/// One abstraction, three families of implementation: deterministic (in-process), LLM-judge
/// (→ eval-runner), and — later — human review (2.1). A scorer carries the <see cref="ScorerDescriptor"/>
/// that is its identity, so its results form a stable score series across runs.
/// </summary>
public interface IScorer
{
    ScorerDescriptor Descriptor { get; }

    Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default);
}
