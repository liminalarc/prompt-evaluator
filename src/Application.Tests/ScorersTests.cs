using Application.Ports;
using Application.Scoring;
using Domain;

namespace Application.Tests;

public class ScorersTests
{
    private static ScoringContext Ctx(
        string modelOutput,
        string? expected = null,
        string input = "in",
        int latencyMs = 100,
        decimal? costUsd = 0.001m)
        => new(input, expected, modelOutput, latencyMs, costUsd);

    private static async Task<ScoreOutcome> Score(IScorer scorer, ScoringContext ctx)
        => await scorer.ScoreAsync(ctx, CancellationToken.None);

    private static readonly ScorerFactory Factory = new();

    private static IScorer Make(ScorerDescriptor d) => Factory.Create(d);

    // ---- Regex ----

    [Fact]
    public async Task Regex_scores_1_on_match_and_0_on_miss()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.Regex, @"^\d+$"));

        var hit = await Score(scorer, Ctx("42"));
        var miss = await Score(scorer, Ctx("forty-two"));

        Assert.Equal(1.0, hit.Value);
        Assert.True(hit.Passed);
        Assert.Equal(0.0, miss.Value);
        Assert.False(miss.Passed);
    }

    // ---- JSON schema ----

    private const string PersonSchema =
        """{"type":"object","required":["name","age"],"properties":{"name":{"type":"string"},"age":{"type":"integer"}}}""";

    [Fact]
    public async Task JsonSchema_passes_conforming_output()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.JsonSchema, PersonSchema));

        var outcome = await Score(scorer, Ctx("""{"name":"Ada","age":36}"""));

        Assert.Equal(1.0, outcome.Value);
        Assert.True(outcome.Passed);
    }

    [Fact]
    public async Task JsonSchema_fails_on_missing_required_field()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.JsonSchema, PersonSchema));

        var outcome = await Score(scorer, Ctx("""{"name":"Ada"}"""));

        Assert.Equal(0.0, outcome.Value);
        Assert.False(outcome.Passed);
        Assert.Contains("age", outcome.Detail);
    }

    [Fact]
    public async Task JsonSchema_fails_on_wrong_type_and_on_malformed_json()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.JsonSchema, PersonSchema));

        var wrongType = await Score(scorer, Ctx("""{"name":"Ada","age":"old"}"""));
        var malformed = await Score(scorer, Ctx("not json at all"));

        Assert.False(wrongType.Passed);
        Assert.False(malformed.Passed);
        Assert.NotNull(malformed.Detail);
    }

    // ---- Exact match ----

    [Fact]
    public async Task ExactMatch_compares_trimmed_output_to_expected()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.ExactMatch));

        var eq = await Score(scorer, Ctx("  hello  ", expected: "hello"));
        var ne = await Score(scorer, Ctx("hello", expected: "goodbye"));

        Assert.Equal(1.0, eq.Value);
        Assert.Equal(0.0, ne.Value);
    }

    [Fact]
    public async Task ExactMatch_fails_when_there_is_no_expected_output()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.ExactMatch));

        var outcome = await Score(scorer, Ctx("anything", expected: null));

        Assert.Equal(0.0, outcome.Value);
        Assert.False(outcome.Passed);
    }

    // ---- Fuzzy match ----

    [Fact]
    public async Task Fuzzy_scores_1_for_identical_and_high_for_close()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch));

        var identical = await Score(scorer, Ctx("kitten", expected: "kitten"));
        var close = await Score(scorer, Ctx("kitten", expected: "sitting"));

        Assert.Equal(1.0, identical.Value);
        Assert.InRange(close.Value, 0.5, 0.99);
    }

    [Fact]
    public async Task Fuzzy_threshold_config_drives_pass_fail()
    {
        // "kitten" vs "sitting" ≈ 0.571 similarity.
        var lenient = Make(ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch, "0.5"));
        var strict = Make(ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch, "0.9"));

        Assert.True((await Score(lenient, Ctx("kitten", expected: "sitting"))).Passed);
        Assert.False((await Score(strict, Ctx("kitten", expected: "sitting"))).Passed);
    }

    // ---- Latency ----

    [Fact]
    public async Task Latency_passes_within_budget_and_reports_the_measurement()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.Latency, "500"));

        var within = await Score(scorer, Ctx("out", latencyMs: 200));
        var over = await Score(scorer, Ctx("out", latencyMs: 900));

        Assert.True(within.Passed);
        Assert.Equal(1.0, within.Value);
        Assert.Contains("200", within.Detail);
        Assert.False(over.Passed);
        Assert.Equal(0.0, over.Value);
    }

    [Fact]
    public async Task Latency_without_budget_is_pure_capture()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.Latency));

        var outcome = await Score(scorer, Ctx("out", latencyMs: 1234));

        Assert.Equal(1.0, outcome.Value);
        Assert.Null(outcome.Passed);
        Assert.Contains("1234", outcome.Detail);
    }

    // ---- Cost ----

    [Fact]
    public async Task Cost_passes_within_budget_and_flags_unknown_cost()
    {
        var scorer = Make(ScorerDescriptor.Deterministic(ScorerKind.Cost, "0.01"));

        var within = await Score(scorer, Ctx("out", costUsd: 0.004m));
        var over = await Score(scorer, Ctx("out", costUsd: 0.02m));
        var unknown = await Score(scorer, Ctx("out", costUsd: null));

        Assert.True(within.Passed);
        Assert.False(over.Passed);
        Assert.Equal(1.0, unknown.Value);
        Assert.Null(unknown.Passed);
    }

    // ---- Factory ----

    [Theory]
    [InlineData(ScorerKind.Regex, "x")]
    [InlineData(ScorerKind.JsonSchema, "{}")]
    [InlineData(ScorerKind.ExactMatch, null)]
    [InlineData(ScorerKind.FuzzyMatch, null)]
    [InlineData(ScorerKind.Latency, null)]
    [InlineData(ScorerKind.Cost, null)]
    public void Factory_builds_a_scorer_whose_descriptor_round_trips(ScorerKind kind, string? config)
    {
        var descriptor = ScorerDescriptor.Deterministic(kind, config);

        var scorer = Factory.Create(descriptor);

        Assert.Equal(descriptor, scorer.Descriptor);
    }

    [Fact]
    public void Factory_rejects_llm_judge_until_the_eval_runner_seam_is_wired()
    {
        // The LLM-judge scorer needs IEvaluationRunner and is built in slice 4.
        var descriptor = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");

        Assert.Throws<NotSupportedException>(() => Factory.Create(descriptor));
    }
}
