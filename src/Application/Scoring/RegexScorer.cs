using System.Text.RegularExpressions;
using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>Passes when the model output matches the configured regular expression.</summary>
public sealed class RegexScorer : IScorer
{
    private readonly Regex _regex;

    public RegexScorer(ScorerDescriptor descriptor)
    {
        Descriptor = descriptor;
        // A bounded match timeout guards against pathological patterns in operator-supplied config.
        _regex = new Regex(descriptor.Config, RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    public ScorerDescriptor Descriptor { get; }

    public Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        var matched = _regex.IsMatch(context.ModelOutput);
        return Task.FromResult(new ScoreOutcome(matched ? 1.0 : 0.0, matched, matched ? null : "no match"));
    }
}
