using Domain;

namespace Domain.Tests;

public class OrganizationTests
{
    [Fact]
    public void Create_sets_name_and_generates_id()
    {
        var org = Organization.Create("Acme");

        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("Acme", org.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => Organization.Create(name!));
    }

    [Fact]
    public void Rename_changes_the_name()
    {
        var org = Organization.Create("Acme");

        org.Rename("Acme Inc");

        Assert.Equal("Acme Inc", org.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_rejects_a_blank_name(string? name)
    {
        var org = Organization.Create("Acme");
        Assert.Throws<ArgumentException>(() => org.Rename(name!));
    }
}
