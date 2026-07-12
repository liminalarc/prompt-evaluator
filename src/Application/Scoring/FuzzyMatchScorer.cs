using System.Globalization;
using Application.Ports;
using Domain;

namespace Application.Scoring;

/// <summary>
/// Scores the Levenshtein similarity of the model output to the fixture's expected output
/// (1.0 = identical, 0.0 = wholly different). The optional config is a pass threshold in [0, 1]
/// (default 0.8) that drives <see cref="ScoreOutcome.Passed"/>.
/// </summary>
public sealed class FuzzyMatchScorer : IScorer
{
    private const double DefaultThreshold = 0.8;
    private readonly double _threshold;

    public FuzzyMatchScorer(ScorerDescriptor descriptor)
    {
        Descriptor = descriptor;
        _threshold = descriptor.Config.Length > 0
            && double.TryParse(descriptor.Config, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
                ? Math.Clamp(t, 0, 1)
                : DefaultThreshold;
    }

    public ScorerDescriptor Descriptor { get; }

    public Task<ScoreOutcome> ScoreAsync(ScoringContext context, CancellationToken ct = default)
    {
        if (context.ExpectedOutput is null)
            return Task.FromResult(new ScoreOutcome(0.0, false, "no expected output to compare against"));

        var similarity = Similarity(context.ModelOutput.Trim(), context.ExpectedOutput.Trim());
        var passed = similarity >= _threshold;
        return Task.FromResult(new ScoreOutcome(
            similarity, passed, $"similarity {similarity:0.###} (threshold {_threshold:0.###})"));
    }

    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        var distance = Levenshtein(a, b);
        var longest = Math.Max(a.Length, b.Length);
        return 1.0 - (double)distance / longest;
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
