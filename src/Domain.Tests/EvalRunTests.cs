using Domain;

namespace Domain.Tests;

public class EvalRunTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_fields_and_generates_id()
    {
        var run = EvalRun.Create("hello", "hello", When);

        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal("hello", run.Prompt);
        Assert.Equal("hello", run.Output);
        Assert.Equal(When, run.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_prompt(string? prompt)
    {
        Assert.Throws<ArgumentException>(() => EvalRun.Create(prompt!, "out", When));
    }

    [Fact]
    public void Create_rejects_null_output()
    {
        Assert.Throws<ArgumentNullException>(() => EvalRun.Create("hi", null!, When));
    }
}
