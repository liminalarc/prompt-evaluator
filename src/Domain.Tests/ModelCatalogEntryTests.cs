namespace Domain.Tests;

public class ModelCatalogEntryTests
{
    private static readonly IReadOnlyCollection<ModelRole> AllRoles =
        new[] { ModelRole.Subject, ModelRole.Judge, ModelRole.Generator };

    [Fact]
    public void Create_sets_fields_generates_id_and_is_active()
    {
        var entry = ModelCatalogEntry.Create(
            "gpt-4o-mini", "GPT-4o mini", ModelProvider.OpenAi,
            new[] { ModelRole.Subject, ModelRole.Judge }, inputPricePerMTokUsd: 0.15m, outputPricePerMTokUsd: 0.6m);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal("gpt-4o-mini", entry.ModelId);
        Assert.Equal("GPT-4o mini", entry.DisplayName);
        Assert.Equal(ModelProvider.OpenAi, entry.Provider);
        Assert.Equal(0.15m, entry.InputPricePerMTokUsd);
        Assert.Equal(0.6m, entry.OutputPricePerMTokUsd);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Create_derives_the_roles_list_from_the_flags_in_order()
    {
        var entry = ModelCatalogEntry.Create(
            "claude-sonnet-5", "Claude Sonnet 5", ModelProvider.Anthropic,
            new[] { ModelRole.Judge, ModelRole.Subject });

        Assert.True(entry.CanSubject);
        Assert.True(entry.CanJudge);
        Assert.False(entry.CanGenerate);
        Assert.Equal(new[] { ModelRole.Subject, ModelRole.Judge }, entry.Roles);
    }

    [Fact]
    public void Create_leaves_prices_null_when_omitted()
    {
        var entry = ModelCatalogEntry.Create("gpt-4o", "GPT-4o", ModelProvider.OpenAi, AllRoles);

        Assert.Null(entry.InputPricePerMTokUsd);
        Assert.Null(entry.OutputPricePerMTokUsd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_model_id(string? modelId)
    {
        Assert.Throws<ArgumentException>(() =>
            ModelCatalogEntry.Create(modelId!, "Name", ModelProvider.Anthropic, AllRoles));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_display_name(string? displayName)
    {
        Assert.Throws<ArgumentException>(() =>
            ModelCatalogEntry.Create("m", displayName!, ModelProvider.Anthropic, AllRoles));
    }

    [Fact]
    public void Create_rejects_an_empty_role_set()
    {
        Assert.Throws<ArgumentException>(() =>
            ModelCatalogEntry.Create("m", "Name", ModelProvider.Anthropic, Array.Empty<ModelRole>()));
    }

    [Fact]
    public void Create_rejects_a_negative_price()
    {
        Assert.Throws<ArgumentException>(() =>
            ModelCatalogEntry.Create("m", "Name", ModelProvider.Anthropic, AllRoles, inputPricePerMTokUsd: -1m));
    }

    [Fact]
    public void Update_changes_the_mutable_fields_but_not_the_model_id()
    {
        var entry = ModelCatalogEntry.Create("gpt-4o", "GPT-4o", ModelProvider.OpenAi, AllRoles);

        entry.Update("GPT-4o (renamed)", ModelProvider.OpenAi, new[] { ModelRole.Judge }, 2.5m, 10m);

        Assert.Equal("gpt-4o", entry.ModelId);
        Assert.Equal("GPT-4o (renamed)", entry.DisplayName);
        Assert.Equal(new[] { ModelRole.Judge }, entry.Roles);
        Assert.False(entry.CanSubject);
        Assert.Equal(2.5m, entry.InputPricePerMTokUsd);
        Assert.Equal(10m, entry.OutputPricePerMTokUsd);
    }

    [Fact]
    public void Update_rejects_an_empty_role_set()
    {
        var entry = ModelCatalogEntry.Create("gpt-4o", "GPT-4o", ModelProvider.OpenAi, AllRoles);

        Assert.Throws<ArgumentException>(() =>
            entry.Update("GPT-4o", ModelProvider.OpenAi, Array.Empty<ModelRole>(), null, null));
    }

    [Fact]
    public void Deactivate_then_activate_toggles_is_active()
    {
        var entry = ModelCatalogEntry.Create("gpt-4o", "GPT-4o", ModelProvider.OpenAi, AllRoles);

        entry.Deactivate();
        Assert.False(entry.IsActive);

        entry.Activate();
        Assert.True(entry.IsActive);
    }
}
