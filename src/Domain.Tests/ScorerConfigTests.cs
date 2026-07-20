using Domain;

namespace Domain.Tests;

public class ScorerConfigTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_binds_a_scorer_to_a_dataset()
    {
        var datasetId = Guid.NewGuid();
        var scorer = ScorerDescriptor.LlmJudge("Rate quality 0-1", "claude-opus-4-8");

        var config = ScorerConfig.Create(datasetId, scorer, When);

        Assert.NotEqual(Guid.Empty, config.Id);
        Assert.Equal(datasetId, config.DatasetId);
        Assert.Equal(scorer, config.Scorer);
        Assert.Equal(When, config.CreatedAt);
    }

    [Fact]
    public void Create_rejects_empty_dataset_id()
    {
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.ExactMatch);

        Assert.Throws<ArgumentException>(() => ScorerConfig.Create(Guid.Empty, scorer, When));
    }

    [Fact]
    public void Create_rejects_a_null_scorer()
    {
        Assert.Throws<ArgumentNullException>(() => ScorerConfig.Create(Guid.NewGuid(), null!, When));
    }

    [Fact]
    public void Create_defaults_the_weight_to_one_so_scorers_start_equally_weighted()
    {
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.ExactMatch);

        var config = ScorerConfig.Create(Guid.NewGuid(), scorer, When);

        Assert.Equal(1.0, config.Weight);
    }

    [Fact]
    public void Create_accepts_an_explicit_weight()
    {
        var scorer = ScorerDescriptor.LlmJudge("Rate quality 0-1", "claude-opus-4-8");

        var config = ScorerConfig.Create(Guid.NewGuid(), scorer, When, weight: 4.0);

        Assert.Equal(4.0, config.Weight);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Create_rejects_a_non_positive_or_non_finite_weight(double weight)
    {
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.ExactMatch);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScorerConfig.Create(Guid.NewGuid(), scorer, When, weight));
    }

    [Fact]
    public void SetWeight_replaces_the_weight()
    {
        var config = ScorerConfig.Create(Guid.NewGuid(), ScorerDescriptor.Deterministic(ScorerKind.ExactMatch), When);

        config.SetWeight(2.5);

        Assert.Equal(2.5, config.Weight);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void SetWeight_rejects_a_non_positive_or_non_finite_weight(double weight)
    {
        var config = ScorerConfig.Create(Guid.NewGuid(), ScorerDescriptor.Deterministic(ScorerKind.ExactMatch), When);

        Assert.Throws<ArgumentOutOfRangeException>(() => config.SetWeight(weight));
    }

    [Fact]
    public void Reweighting_does_not_change_the_scorer_identity_so_the_score_series_is_stable()
    {
        var scorer = ScorerDescriptor.LlmJudge("Rate quality 0-1", "claude-opus-4-8");
        var config = ScorerConfig.Create(Guid.NewGuid(), scorer, When);
        var identityBefore = config.Scorer.Identity;

        config.SetWeight(7.0);

        Assert.Equal(identityBefore, config.Scorer.Identity);
    }
}
