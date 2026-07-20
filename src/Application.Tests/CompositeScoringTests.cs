using Application.Analytics;

namespace Application.Tests;

public class CompositeScoringTests
{
    [Fact]
    public void No_scorers_has_no_composite()
    {
        Assert.Null(CompositeScoring.WeightedComposite(
            new Dictionary<string, double>(), new Dictionary<string, double>()));
    }

    [Fact]
    public void Equal_weights_reduce_to_a_plain_mean()
    {
        var means = new Dictionary<string, double> { ["a"] = 0.4, ["b"] = 0.8 };
        var weights = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 1.0 };

        Assert.Equal(0.6, CompositeScoring.WeightedComposite(means, weights)!.Value, 9);
    }

    [Fact]
    public void A_higher_weight_scorer_dominates_the_composite()
    {
        var means = new Dictionary<string, double> { ["regex"] = 0.4, ["judge"] = 0.9 };
        var weights = new Dictionary<string, double> { ["regex"] = 1.0, ["judge"] = 4.0 };

        // (1*0.4 + 4*0.9) / 5 = 0.8 — pulled toward the heavily-weighted judge, not the midpoint 0.65.
        Assert.Equal(0.8, CompositeScoring.WeightedComposite(means, weights)!.Value, 9);
    }

    [Fact]
    public void A_scorer_without_a_configured_weight_falls_back_to_one()
    {
        var means = new Dictionary<string, double> { ["known"] = 0.4, ["orphan"] = 1.0 };
        var weights = new Dictionary<string, double> { ["known"] = 3.0 }; // orphan has no weight row

        // (3*0.4 + 1*1.0) / 4 = 0.55
        Assert.Equal(0.55, CompositeScoring.WeightedComposite(means, weights)!.Value, 9);
    }

    [Fact]
    public void Weights_renormalize_when_a_scorer_is_added_or_removed()
    {
        var weights = new Dictionary<string, double> { ["judge"] = 4.0, ["regex"] = 1.0 };

        // Only the judge present → the composite is exactly the judge mean (renormalized over w=4).
        var judgeOnly = new Dictionary<string, double> { ["judge"] = 0.8 };
        Assert.Equal(0.8, CompositeScoring.WeightedComposite(judgeOnly, weights)!.Value, 9);

        // Add a regex score → (4*0.8 + 1*0.4)/5 = 0.72.
        var both = new Dictionary<string, double> { ["judge"] = 0.8, ["regex"] = 0.4 };
        Assert.Equal(0.72, CompositeScoring.WeightedComposite(both, weights)!.Value, 9);

        // Remove the judge → the composite is exactly the regex mean (renormalized over w=1).
        var regexOnly = new Dictionary<string, double> { ["regex"] = 0.4 };
        Assert.Equal(0.4, CompositeScoring.WeightedComposite(regexOnly, weights)!.Value, 9);
    }
}
