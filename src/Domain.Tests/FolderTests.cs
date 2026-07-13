using Domain;

namespace Domain.Tests;

public class FolderTests
{
    [Fact]
    public void CreateRoot_is_top_level_with_no_parent()
    {
        var folder = Folder.CreateRoot("Stormboard");

        Assert.NotEqual(Guid.Empty, folder.Id);
        Assert.Equal("Stormboard", folder.Name);
        Assert.Null(folder.ParentId);
        Assert.True(folder.IsTopLevel);
    }

    [Fact]
    public void CreateChild_hangs_off_its_parent_and_is_not_top_level()
    {
        var parent = Folder.CreateRoot("Stormboard");

        var child = Folder.CreateChild("Summarization", parent.Id);

        Assert.NotEqual(Guid.Empty, child.Id);
        Assert.NotEqual(parent.Id, child.Id);
        Assert.Equal("Summarization", child.Name);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.False(child.IsTopLevel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRoot_rejects_blank_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => Folder.CreateRoot(name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateChild_rejects_blank_name(string? name)
    {
        var parent = Folder.CreateRoot("Stormboard");
        Assert.Throws<ArgumentException>(() => Folder.CreateChild(name!, parent.Id));
    }

    [Fact]
    public void CreateChild_rejects_empty_parent_id()
    {
        Assert.Throws<ArgumentException>(() => Folder.CreateChild("Summarization", Guid.Empty));
    }

    [Fact]
    public void Rename_changes_the_name()
    {
        var folder = Folder.CreateRoot("Stormbaord");

        folder.Rename("Stormboard");

        Assert.Equal("Stormboard", folder.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_rejects_blank_name(string? name)
    {
        var folder = Folder.CreateRoot("Stormboard");
        Assert.Throws<ArgumentException>(() => folder.Rename(name!));
    }

    [Fact]
    public void MoveTo_reparents_under_another_folder()
    {
        var newParent = Folder.CreateRoot("Platform");
        var folder = Folder.CreateChild("Summarization", Guid.NewGuid());

        folder.MoveTo(newParent.Id);

        Assert.Equal(newParent.Id, folder.ParentId);
        Assert.False(folder.IsTopLevel);
    }

    [Fact]
    public void MoveTo_null_promotes_a_folder_to_top_level()
    {
        var folder = Folder.CreateChild("Summarization", Guid.NewGuid());

        folder.MoveTo(null);

        Assert.Null(folder.ParentId);
        Assert.True(folder.IsTopLevel);
    }

    [Fact]
    public void MoveTo_rejects_making_a_folder_its_own_parent()
    {
        var folder = Folder.CreateRoot("Stormboard");

        Assert.Throws<ArgumentException>(() => folder.MoveTo(folder.Id));
    }
}
