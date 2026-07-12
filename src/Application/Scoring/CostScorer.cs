using System.Globalization;
using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Captures the fixture's execution cost in USD. With a config budget it passes when the cost is
/// within budget; with no config, or when the cost is unknown, it is a pure capture (value 1.0).
/// </summary>
public sealed class CostScorer : IScorer
{
    private readonly decimal? _budgetUsd;

    public CostScorer(ScorerDescriptor descriptor)
    {
        Descriptor = descriptor;
        _budgetUsd = descriptor.Config.Length > 0
            && decimal.TryParse(descriptor.Config, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                ? b
                : null;
    }

    public ScorerDescriptor Descriptor { get; }

    public Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        if (context.CostUsd is not decimal cost)
            return Task.FromResult(new ScoreOutcome(1.0, null, "cost unknown"));

        if (_budgetUsd is null)
            return Task.FromResult(new ScoreOutcome(1.0, null, $"${cost}"));

        var within = cost <= _budgetUsd.Value;
        return Task.FromResult(new ScoreOutcome(
            within ? 1.0 : 0.0, within, $"${cost} (budget ${_budgetUsd.Value})"));
    }
}
