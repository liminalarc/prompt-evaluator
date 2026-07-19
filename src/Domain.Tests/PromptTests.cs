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

    [Fact]
    public void Create_leaves_current_version_unset()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        Assert.Null(prompt.CurrentVersionId);
        Assert.Null(prompt.CurrentVersionSha);
        Assert.Null(prompt.CurrentVersionSetAt);
    }

    [Fact]
    public void SetCurrentVersion_marks_a_version_in_history_with_sha_and_timestamp()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        var v1 = prompt.AddVersion("Do the thing", "claude-sonnet-5", When);

        var ok = prompt.SetCurrentVersion(v1.Id, "abc1234", When);

        Assert.True(ok);
        Assert.Equal(v1.Id, prompt.CurrentVersionId);
        Assert.Equal("abc1234", prompt.CurrentVersionSha);
        Assert.Equal(When, prompt.CurrentVersionSetAt);
    }

    [Fact]
    public void SetCurrentVersion_moves_the_marker_so_only_one_is_current()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When);

        prompt.SetCurrentVersion(v1.Id, null, When);
        prompt.SetCurrentVersion(v2.Id, null, When.AddDays(1)); // mark-as-backported → move forward

        Assert.Equal(v2.Id, prompt.CurrentVersionId);
        Assert.Equal(When.AddDays(1), prompt.CurrentVersionSetAt);
    }

    [Fact]
    public void SetCurrentVersion_rejects_a_version_not_in_this_prompt()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        prompt.AddVersion("v1", "claude-sonnet-5", When);

        var ok = prompt.SetCurrentVersion(Guid.NewGuid(), null, When);

        Assert.False(ok);
        Assert.Null(prompt.CurrentVersionId);
    }

    [Fact]
    public void SetCurrentVersion_normalizes_a_blank_sha_to_null()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);

        prompt.SetCurrentVersion(v1.Id, "   ", When);

        Assert.Null(prompt.CurrentVersionSha);
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
    public void EditVersionLabel_updates_only_the_label_leaving_content_and_model_immutable()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        var v1 = prompt.AddVersion("Summarize: {input}", "claude-opus-4-8", When, label: "baseline");

        var ok = prompt.EditVersionLabel(v1.Id, "renamed baseline");

        Assert.True(ok);
        var version = Assert.Single(prompt.Versions);
        Assert.Equal("renamed baseline", version.Label);
        Assert.Equal("Summarize: {input}", version.Content); // immutable
        Assert.Equal("claude-opus-4-8", version.TargetModel); // immutable
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EditVersionLabel_normalizes_a_blank_label_to_null(string? label)
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        var v1 = prompt.AddVersion("Summarize: {input}", "claude-sonnet-5", When, label: "baseline");

        prompt.EditVersionLabel(v1.Id, label);

        Assert.Null(Assert.Single(prompt.Versions).Label);
    }

    [Fact]
    public void EditVersionLabel_returns_false_for_a_version_not_in_this_prompt()
    {
        var prompt = Prompt.Create(OrgId, "Summarizer");
        prompt.AddVersion("Summarize: {input}", "claude-sonnet-5", When);

        Assert.False(prompt.EditVersionLabel(Guid.NewGuid(), "x"));
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
