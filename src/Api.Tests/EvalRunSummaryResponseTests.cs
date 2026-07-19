using Api.EvalRuns;
using Domain;

namespace Api.Tests;

public class EvalRunSummaryResponseTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MeanScore_uses_the_llm_judge_and_ignores_inflating_deterministic_scorers()
    {
        // A run scores each fixture with an LLM judge (the quality signal) + a Regex (near-always 1.0).
        // The headline MeanScore must reflect the judge, not be inflated to ~1.0 by the Regex (2.19 W23/W30/W33).
        var run = EvalRun.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), When);
        var judge = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "[0-9]");

        var f0 = run.RecordFixture(Guid.NewGuid(), "out0", 10, 0, 0, 0.001m);
        f0.AddScore(judge, 0.8, true, "clean");
        f0.AddScore(regex, 1.0, true, null);
        var f1 = run.RecordFixture(Guid.NewGuid(), "out1", 10, 0, 0, 0.001m);
        f1.AddScore(judge, 0.9, true, "good");
        f1.AddScore(regex, 1.0, true, null);

        var summary = EvalRunSummaryResponse.From(run);

        Assert.Equal(0.85, summary.MeanScore!.Value, 3); // (0.8 + 0.9)/2 — NOT dragged up by the 1.0 Regex
        Assert.Equal("LlmJudge", summary.MeanScorerKind);
        Assert.Equal(4, summary.ScoreCount);
        Assert.Equal(2, summary.FixtureCount);
    }

    [Fact]
    public void MeanScore_falls_back_to_the_overall_mean_when_there_is_no_judge()
    {
        var run = EvalRun.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), When);
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "[0-9]");
        run.RecordFixture(Guid.NewGuid(), "o", 10, 0, 0, 0.001m).AddScore(regex, 1.0, true, null);

        var summary = EvalRunSummaryResponse.From(run);

        Assert.Equal(1.0, summary.MeanScore!.Value, 3);
        Assert.Equal("overall", summary.MeanScorerKind);
    }

    [Fact]
    public void MeanScore_is_null_for_a_run_with_no_scores()
    {
        var run = EvalRun.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), When);
        run.RecordFixture(Guid.NewGuid(), "o", 10, 0, 0, 0.001m);

        var summary = EvalRunSummaryResponse.From(run);

        Assert.Null(summary.MeanScore);
        Assert.Null(summary.MeanScorerKind);
    }
}
