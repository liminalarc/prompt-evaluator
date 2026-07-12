using Domain;

namespace Domain.Tests;

public class ScorerDescriptorTests
{
    [Fact]
    public void Deterministic_sets_kind_and_config_with_no_judge_model()
    {
        var d = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^ok$");

        Assert.Equal(ScorerKind.Regex, d.Kind);
        Assert.Equal("^ok$", d.Config);
        Assert.Null(d.JudgeModel);
    }

    [Fact]
    public void LlmJudge_sets_kind_rubric_and_judge_model()
    {
        var d = ScorerDescriptor.LlmJudge("Rate helpfulness 0-1", "claude-opus-4-8");

        Assert.Equal(ScorerKind.LlmJudge, d.Kind);
        Assert.Equal("Rate helpfulness 0-1", d.Config);
        Assert.Equal("claude-opus-4-8", d.JudgeModel);
    }

    [Fact]
    public void Deterministic_rejects_the_llm_judge_kind()
    {
        Assert.Throws<ArgumentException>(() => ScorerDescriptor.Deterministic(ScorerKind.LlmJudge, "x"));
    }

    [Theory]
    [InlineData(ScorerKind.Regex)]
    [InlineData(ScorerKind.JsonSchema)]
    public void Deterministic_requires_config_for_pattern_kinds(ScorerKind kind)
    {
        Assert.Throws<ArgumentException>(() => ScorerDescriptor.Deterministic(kind, null));
        Assert.Throws<ArgumentException>(() => ScorerDescriptor.Deterministic(kind, "   "));
    }

    [Theory]
    [InlineData(ScorerKind.ExactMatch)]
    [InlineData(ScorerKind.FuzzyMatch)]
    [InlineData(ScorerKind.Latency)]
    [InlineData(ScorerKind.Cost)]
    public void Deterministic_allows_optional_config_for_other_kinds(ScorerKind kind)
    {
        var d = ScorerDescriptor.Deterministic(kind);

        Assert.Equal(kind, d.Kind);
        Assert.Equal(string.Empty, d.Config);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LlmJudge_rejects_blank_rubric(string? rubric)
    {
        Assert.Throws<ArgumentException>(() => ScorerDescriptor.LlmJudge(rubric!, "claude-opus-4-8"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LlmJudge_rejects_blank_judge_model(string? model)
    {
        Assert.Throws<ArgumentException>(() => ScorerDescriptor.LlmJudge("rubric", model!));
    }

    [Fact]
    public void Identity_is_stable_for_the_same_descriptor()
    {
        var a = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");
        var b = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");

        Assert.Equal(a.Identity, b.Identity);
    }

    [Fact]
    public void Identity_differs_when_only_the_judge_model_differs()
    {
        // AC #7: two runs judged by different models record as different scorer series.
        var opus = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");
        var haiku = ScorerDescriptor.LlmJudge("rubric", "claude-haiku-4-5");

        Assert.Equal(opus.Kind, haiku.Kind);
        Assert.NotEqual(opus.Identity, haiku.Identity);
    }

    [Fact]
    public void Identity_differs_across_kinds_and_configs()
    {
        var regexA = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^a$");
        var regexB = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^b$");
        var exact = ScorerDescriptor.Deterministic(ScorerKind.ExactMatch);

        Assert.NotEqual(regexA.Identity, regexB.Identity);
        Assert.NotEqual(regexA.Identity, exact.Identity);
    }

    [Fact]
    public void Descriptors_with_the_same_values_are_equal()
    {
        var a = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch, "0.8");
        var b = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch, "0.8");

        Assert.Equal(a, b);
    }
}
