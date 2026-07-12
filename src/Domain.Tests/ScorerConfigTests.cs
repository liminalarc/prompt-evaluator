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
}
