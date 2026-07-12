using Domain;

namespace Domain.Tests;

public class EvalRunTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PromptId = Guid.NewGuid();
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid DatasetId = Guid.NewGuid();

    private static EvalRun NewRun() => EvalRun.Create(PromptId, VersionId, DatasetId, When);

    [Fact]
    public void Create_sets_identity_fields_and_starts_empty()
    {
        var run = NewRun();

        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(PromptId, run.PromptId);
        Assert.Equal(VersionId, run.PromptVersionId);
        Assert.Equal(DatasetId, run.DatasetId);
        Assert.Equal(When, run.CreatedAt);
        Assert.Empty(run.Results);
    }

    [Fact]
    public void Create_rejects_empty_identity_guids()
    {
        Assert.Throws<ArgumentException>(() => EvalRun.Create(Guid.Empty, VersionId, DatasetId, When));
        Assert.Throws<ArgumentException>(() => EvalRun.Create(PromptId, Guid.Empty, DatasetId, When));
        Assert.Throws<ArgumentException>(() => EvalRun.Create(PromptId, VersionId, Guid.Empty, When));
    }

    [Fact]
    public void RecordFixture_appends_a_result_and_returns_it()
    {
        var run = NewRun();
        var fixtureId = Guid.NewGuid();

        var result = run.RecordFixture(fixtureId, "the model output", latencyMs: 1234, costUsd: 0.0021m);

        var only = Assert.Single(run.Results);
        Assert.Same(result, only);
        Assert.Equal(fixtureId, only.FixtureId);
        Assert.Equal("the model output", only.ModelOutput);
        Assert.Equal(1234, only.LatencyMs);
        Assert.Equal(0.0021m, only.CostUsd);
        Assert.Empty(only.Scores);
    }

    [Fact]
    public void RecordFixture_allows_empty_output_and_null_cost()
    {
        // A subject model can legitimately return an empty string; cost may be unknown.
        var run = NewRun();

        var result = run.RecordFixture(Guid.NewGuid(), string.Empty, latencyMs: 0, costUsd: null);

        Assert.Equal(string.Empty, result.ModelOutput);
        Assert.Null(result.CostUsd);
    }

    [Fact]
    public void RecordFixture_rejects_empty_fixture_id_null_output_and_negatives()
    {
        var run = NewRun();

        Assert.Throws<ArgumentException>(() => run.RecordFixture(Guid.Empty, "out", 1, null));
        Assert.Throws<ArgumentNullException>(() => run.RecordFixture(Guid.NewGuid(), null!, 1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.RecordFixture(Guid.NewGuid(), "out", -1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.RecordFixture(Guid.NewGuid(), "out", 1, -0.01m));
    }

    [Fact]
    public void AddScore_appends_one_score_per_scorer_and_they_compose()
    {
        // AC #4: scorers compose — deterministic + llm-judge produce distinct scores per fixture.
        var run = NewRun();
        var fixture = run.RecordFixture(Guid.NewGuid(), "42", latencyMs: 10, costUsd: null);

        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, @"^\d+$");
        var judge = ScorerDescriptor.LlmJudge("Is the answer correct?", "claude-opus-4-8");

        fixture.AddScore(regex, value: 1.0, passed: true, detail: null);
        fixture.AddScore(judge, value: 0.75, passed: null, detail: "mostly right");

        Assert.Equal(2, fixture.Scores.Count);
        Assert.Contains(fixture.Scores, s => s.Scorer == regex && s.Value == 1.0 && s.Passed == true);
        Assert.Contains(fixture.Scores, s => s.Scorer == judge && s.Value == 0.75 && s.Detail == "mostly right");
        Assert.Equal(2, fixture.Scores.Select(s => s.Scorer.Identity).Distinct().Count());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void AddScore_rejects_value_outside_the_unit_interval(double value)
    {
        var run = NewRun();
        var fixture = run.RecordFixture(Guid.NewGuid(), "out", 1, null);
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.ExactMatch);

        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.AddScore(scorer, value, null, null));
    }

    [Fact]
    public void Results_and_scores_are_append_only_and_grow()
    {
        var run = NewRun();

        run.RecordFixture(Guid.NewGuid(), "a", 1, null);
        run.RecordFixture(Guid.NewGuid(), "b", 1, null);

        Assert.Equal(2, run.Results.Count);
        // The exposed collections are read-only snapshots — no external mutation path.
        Assert.IsAssignableFrom<IReadOnlyList<FixtureRun>>(run.Results);
        Assert.IsAssignableFrom<IReadOnlyList<Score>>(run.Results[0].Scores);
    }
}
