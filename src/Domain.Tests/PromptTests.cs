using Domain;

namespace Domain.Tests;

public class PromptTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void Create_sets_fields_generates_id_and_starts_with_no_versions()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer", "Summarizes captured SLM output");

        Assert.NotEqual(Guid.Empty, prompt.Id);
        Assert.Equal(OrgId, prompt.OrganizationId);
        Assert.Equal("Summarizer", prompt.Name);
        Assert.Equal("Summarizes captured SLM output", prompt.Description);
        Assert.Empty(prompt.Versions);
    }

    [Fact]
    public void Create_rejects_an_empty_org_id()
    {
        Assert.Throws<ArgumentException>(() => Prompt.Create(Guid.Empty, "Summarizer"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => Prompt.Create(OrgId, name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_normalizes_blank_description_to_null(string? description)
    {
        var prompt = Prompt.Create(OrgId, "Summarizer", description);

        Assert.Null(prompt.Description);
    }

    [Fact]
    public void AddVersion_appends_and_assigns_sequential_version_numbers()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        var v1 = prompt.AddVersion("Summarize: {input}", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("Summarize concisely: {input}", "claude-sonnet-5", When);

        Assert.Equal(1, v1.VersionNumber);
        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(new[] { 1, 2 }, prompt.Versions.Select(v => v.VersionNumber));
    }

    [Fact]
    public void AddVersion_records_content_target_model_label_and_source_app()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        var version = prompt.AddVersion(
            "Summarize: {input}", "claude-opus-4-8", When, label: "baseline", sourceApp: "Stormboard");

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal("Summarize: {input}", version.Content);
        Assert.Equal("claude-opus-4-8", version.TargetModel);
        Assert.Equal("baseline", version.Label);
        Assert.Equal("Stormboard", version.SourceApp);
        Assert.Equal(When, version.CreatedAt);
    }

    [Fact]
    public void AddVersion_normalizes_blank_label_and_source_app_to_null()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        var version = prompt.AddVersion("Summarize: {input}", "claude-sonnet-5", When, label: "   ", sourceApp: "");

        Assert.Null(version.Label);
        Assert.Null(version.SourceApp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddVersion_rejects_blank_content(string? content)
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        Assert.Throws<ArgumentException>(() => prompt.AddVersion(content!, "claude-sonnet-5", When));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddVersion_rejects_blank_target_model(string? targetModel)
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        Assert.Throws<ArgumentException>(() => prompt.AddVersion("Summarize: {input}", targetModel!, When));
    }

    [Fact]
    public void Create_leaves_a_prompt_unfiled_by_default()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        Assert.Null(prompt.FolderId);
    }

    [Fact]
    public void Create_can_file_a_prompt_into_a_folder()
    {
        var folderId = Guid.NewGuid();

        var prompt = Prompt.Create(OrgId, "Summarizer", folderId: folderId);

        Assert.Equal(folderId, prompt.FolderId);
    }

    [Fact]
    public void MoveToFolder_files_the_prompt()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        var folderId = Guid.NewGuid();

        prompt.MoveToFolder(folderId);

        Assert.Equal(folderId, prompt.FolderId);
    }

    [Fact]
    public void MoveToFolder_rejects_an_empty_folder_id()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");

        Assert.Throws<ArgumentException>(() => prompt.MoveToFolder(Guid.Empty));
    }

    [Fact]
    public void Unfile_moves_the_prompt_back_to_the_root()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer", folderId: Guid.NewGuid());

        prompt.Unfile();

        Assert.Null(prompt.FolderId);
    }

    [Fact]
    public void Versions_is_append_only_from_the_outside()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        prompt.AddVersion("v1", "claude-sonnet-5", When);

        // The exposed collection must not permit external mutation of the aggregate's history.
        Assert.True(prompt.Versions is System.Collections.ObjectModel.ReadOnlyCollection<PromptVersion>
            || prompt.Versions.GetType().Name.Contains("ReadOnly"));
    }
}
