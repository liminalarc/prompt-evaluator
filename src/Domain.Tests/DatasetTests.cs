using Domain;

namespace Domain.Tests;

public class DatasetTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PromptId = Guid.NewGuid();

    [Fact]
    public void Create_sets_fields_generates_id_and_starts_empty()
    {
        var dataset = Dataset.Create(PromptId, "Summaries", "Captured SLM summaries");

        Assert.NotEqual(Guid.Empty, dataset.Id);
        Assert.Equal(PromptId, dataset.PromptId);
        Assert.Equal("Summaries", dataset.Name);
        Assert.Equal("Captured SLM summaries", dataset.Description);
        Assert.Empty(dataset.Fixtures);
    }

    [Fact]
    public void Create_rejects_an_empty_prompt_id()
    {
        // A dataset lives with exactly one prompt (1.7) — it must always name its owner.
        Assert.Throws<ArgumentException>(() => Dataset.Create(Guid.Empty, "Summaries"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => Dataset.Create(PromptId, name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_normalizes_blank_description_to_null(string? description)
    {
        var dataset = Dataset.Create(PromptId, "Summaries", description);

        Assert.Null(dataset.Description);
    }

    [Fact]
    public void AddCapturedFixture_tags_origin_and_records_context_with_no_seed()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");

        var fixture = dataset.AddCapturedFixture(
            "Summarize this thread", When, upstreamContext: "raw SLM output", expectedOutput: "a summary");

        Assert.NotEqual(Guid.Empty, fixture.Id);
        Assert.Equal(FixtureOrigin.Captured, fixture.Origin);
        Assert.Equal("Summarize this thread", fixture.Input);
        Assert.Equal("raw SLM output", fixture.UpstreamContext);
        Assert.Equal("a summary", fixture.ExpectedOutput);
        Assert.Null(fixture.SeedFixtureId);
        Assert.Equal(When, fixture.CreatedAt);
        Assert.Single(dataset.Fixtures);
    }

    [Fact]
    public void AddSyntheticFixture_tags_origin_and_links_the_seed()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");
        var seed = dataset.AddCapturedFixture("captured input", When);

        var synthetic = dataset.AddSyntheticFixture(
            "generated variant", seed.Id, When, upstreamContext: "shaped like SLM output");

        Assert.Equal(FixtureOrigin.Synthetic, synthetic.Origin);
        Assert.Equal("generated variant", synthetic.Input);
        Assert.Equal(seed.Id, synthetic.SeedFixtureId);
        Assert.Equal(2, dataset.Fixtures.Count);
    }

    [Fact]
    public void AddManualFixture_can_mark_synthetic_with_no_seed()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");

        var fixture = dataset.AddManualFixture(
            FixtureOrigin.Synthetic, "hand-written edge case", When, label: "empty thread");

        Assert.Equal(FixtureOrigin.Synthetic, fixture.Origin);
        Assert.Null(fixture.SeedFixtureId); // hand-authored → no seed
        Assert.Equal("empty thread", fixture.Label);
    }

    [Fact]
    public void AddCapturedFixture_records_label_and_description()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");

        var fixture = dataset.AddCapturedFixture(
            "Summarize this", When, label: "mid-cap", description: "improving mid-handicapper");

        Assert.Equal("mid-cap", fixture.Label);
        Assert.Equal("improving mid-handicapper", fixture.Description);
    }

    [Fact]
    public void EditFixtureMetadata_updates_label_and_description_leaving_input_fixed()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");
        var fixture = dataset.AddCapturedFixture("Summarize this", When, label: "old");

        var ok = dataset.EditFixtureMetadata(fixture.Id, "new label", "new description");

        Assert.True(ok);
        Assert.Equal("new label", fixture.Label);
        Assert.Equal("new description", fixture.Description);
        Assert.Equal("Summarize this", fixture.Input); // fixed
    }

    [Fact]
    public void EditFixtureMetadata_returns_false_for_an_unknown_fixture()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");
        dataset.AddCapturedFixture("Summarize this", When);

        Assert.False(dataset.EditFixtureMetadata(Guid.NewGuid(), "x", null));
    }

    [Fact]
    public void AddSyntheticFixture_rejects_an_empty_seed()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");

        Assert.Throws<ArgumentException>(
            () => dataset.AddSyntheticFixture("generated variant", Guid.Empty, When));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddCapturedFixture_rejects_blank_input(string? input)
    {
        var dataset = Dataset.Create(PromptId, "Summaries");

        Assert.Throws<ArgumentException>(() => dataset.AddCapturedFixture(input!, When));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSyntheticFixture_rejects_blank_input(string? input)
    {
        var dataset = Dataset.Create(PromptId, "Summaries");
        var seed = dataset.AddCapturedFixture("captured input", When);

        Assert.Throws<ArgumentException>(() => dataset.AddSyntheticFixture(input!, seed.Id, When));
    }

    [Fact]
    public void AddCapturedFixture_normalizes_blank_context_and_expected_to_null()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");

        var fixture = dataset.AddCapturedFixture("input", When, upstreamContext: "   ", expectedOutput: "");

        Assert.Null(fixture.UpstreamContext);
        Assert.Null(fixture.ExpectedOutput);
    }

    [Fact]
    public void Fixtures_is_append_only_from_the_outside()
    {
        var dataset = Dataset.Create(PromptId, "Summaries");
        dataset.AddCapturedFixture("input", When);

        Assert.True(dataset.Fixtures is System.Collections.ObjectModel.ReadOnlyCollection<Fixture>
            || dataset.Fixtures.GetType().Name.Contains("ReadOnly"));
    }
}
