using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>Passes when the (trimmed) model output equals the fixture's expected output.</summary>
public sealed class ExactMatchScorer(ScorerDescriptor descriptor) : IScorer
{
    public ScorerDescriptor Descriptor { get; } = descriptor;

    public Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        if (context.ExpectedOutput is null)
            return Task.FromResult(new ScoreOutcome(0.0, false, "no expected output to match against"));

        var matched = string.Equals(
            context.ModelOutput.Trim(), context.ExpectedOutput.Trim(), StringComparison.Ordinal);
        return Task.FromResult(new ScoreOutcome(matched ? 1.0 : 0.0, matched, matched ? null : "output differs from expected"));
    }
}
