using Application.Analytics;

namespace Application.Tests;

public class LineDiffTests
{
    [Fact]
    public void Identical_text_is_all_context()
    {
        var diff = LineDiff.Compute("a\nb\nc", "a\nb\nc");

        Assert.All(diff, line => Assert.Equal(DiffLineKind.Context, line.Kind));
        Assert.Equal(["a", "b", "c"], diff.Select(l => l.Text));
    }

    [Fact]
    public void An_added_line_is_marked_added()
    {
        var diff = LineDiff.Compute("a\nc", "a\nb\nc");

        Assert.Equal(
            [
                (DiffLineKind.Context, "a"),
                (DiffLineKind.Added, "b"),
                (DiffLineKind.Context, "c"),
            ],
            diff.Select(l => (l.Kind, l.Text)));
    }

    [Fact]
    public void A_removed_line_is_marked_removed()
    {
        var diff = LineDiff.Compute("a\nb\nc", "a\nc");

        Assert.Equal(
            [
                (DiffLineKind.Context, "a"),
                (DiffLineKind.Removed, "b"),
                (DiffLineKind.Context, "c"),
            ],
            diff.Select(l => (l.Kind, l.Text)));
    }

    [Fact]
    public void A_changed_line_is_a_removal_then_an_addition()
    {
        var diff = LineDiff.Compute("a\nold\nc", "a\nnew\nc");

        Assert.Equal(
            [
                (DiffLineKind.Context, "a"),
                (DiffLineKind.Removed, "old"),
                (DiffLineKind.Added, "new"),
                (DiffLineKind.Context, "c"),
            ],
            diff.Select(l => (l.Kind, l.Text)));
    }

    [Fact]
    public void Empty_to_content_adds_the_new_lines()
    {
        var diff = LineDiff.Compute("", "x\ny");

        // "" splits to a single empty line (removed), then both new lines are added.
        Assert.Equal(
            [
                (DiffLineKind.Removed, ""),
                (DiffLineKind.Added, "x"),
                (DiffLineKind.Added, "y"),
            ],
            diff.Select(l => (l.Kind, l.Text)));
    }
}
