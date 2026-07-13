using Domain;

namespace Domain.Tests;

public class FolderTests
{
    private static readonly Guid OrgId = Guid.NewGuid();

    [Fact]
    public void CreateRoot_is_top_level_with_no_parent_and_belongs_to_the_org()
    {
        var folder = Folder.CreateRoot(OrgId, "Stormboard");

        Assert.NotEqual(Guid.Empty, folder.Id);
        Assert.Equal(OrgId, folder.OrganizationId);
        Assert.Equal("Stormboard", folder.Name);
        Assert.Null(folder.ParentId);
        Assert.True(folder.IsTopLevel);
    }

    [Fact]
    public void CreateChild_hangs_off_its_parent_and_is_not_top_level()
    {
        var parent = Folder.CreateRoot(OrgId, "Stormboard");

        var child = Folder.CreateChild(OrgId, "Summarization", parent.Id);

        Assert.NotEqual(Guid.Empty, child.Id);
        Assert.NotEqual(parent.Id, child.Id);
        Assert.Equal(OrgId, child.OrganizationId);
        Assert.Equal("Summarization", child.Name);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.False(child.IsTopLevel);
    }

    [Fact]
    public void CreateRoot_rejects_an_empty_org_id()
    {
        Assert.Throws<ArgumentException>(() => Folder.CreateRoot(Guid.Empty, "Stormboard"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRoot_rejects_blank_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => Folder.CreateRoot(OrgId, name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateChild_rejects_blank_name(string? name)
    {
        var parent = Folder.CreateRoot(OrgId, "Stormboard");
        Assert.Throws<ArgumentException>(() => Folder.CreateChild(OrgId, name!, parent.Id));
    }

    [Fact]
    public void CreateChild_rejects_empty_parent_id()
    {
        Assert.Throws<ArgumentException>(() => Folder.CreateChild(OrgId, "Summarization", Guid.Empty));
    }

    [Fact]
    public void Rename_changes_the_name()
    {
        var folder = Folder.CreateRoot(OrgId, "Stormbaord");

        folder.Rename("Stormboard");

        Assert.Equal("Stormboard", folder.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_rejects_blank_name(string? name)
    {
        var folder = Folder.CreateRoot(OrgId, "Stormboard");
        Assert.Throws<ArgumentException>(() => folder.Rename(name!));
    }

    [Fact]
    public void MoveTo_reparents_under_another_folder()
    {
        var newParent = Folder.CreateRoot(OrgId, "Platform");
        var folder = Folder.CreateChild(OrgId, "Summarization", Guid.NewGuid());

        folder.MoveTo(newParent.Id);

        Assert.Equal(newParent.Id, folder.ParentId);
        Assert.False(folder.IsTopLevel);
    }

    [Fact]
    public void MoveTo_null_promotes_a_folder_to_top_level()
    {
        var folder = Folder.CreateChild(OrgId, "Summarization", Guid.NewGuid());

        folder.MoveTo(null);

        Assert.Null(folder.ParentId);
        Assert.True(folder.IsTopLevel);
    }

    [Fact]
    public void MoveTo_rejects_making_a_folder_its_own_parent()
    {
        var folder = Folder.CreateRoot(OrgId, "Stormboard");

        Assert.Throws<ArgumentException>(() => folder.MoveTo(folder.Id));
    }
}
