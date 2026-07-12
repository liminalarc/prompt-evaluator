using System.Globalization;
using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Captures the fixture's execution latency. With a config budget (milliseconds) it passes when the
/// latency is within budget; with no config it is a pure capture (value 1.0, no pass/fail).
/// </summary>
public sealed class LatencyScorer : IScorer
{
    private readonly int? _budgetMs;

    public LatencyScorer(ScorerDescriptor descriptor)
    {
        Descriptor = descriptor;
        _budgetMs = descriptor.Config.Length > 0
            && int.TryParse(descriptor.Config, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
                ? b
                : null;
    }

    public ScorerDescriptor Descriptor { get; }

    public Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        var latency = context.LatencyMs;
        if (_budgetMs is null)
            return Task.FromResult(new ScoreOutcome(1.0, null, $"{latency}ms"));

        var within = latency <= _budgetMs.Value;
        return Task.FromResult(new ScoreOutcome(
            within ? 1.0 : 0.0, within, $"{latency}ms (budget {_budgetMs.Value}ms)"));
    }
}
