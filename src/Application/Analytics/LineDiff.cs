namespace Application.Analytics;

/// <summary>Whether a diff line is unchanged (<see cref="Context"/>), only in the new text
/// (<see cref="Added"/>), or only in the old text (<see cref="Removed"/>).</summary>
public enum DiffLineKind
{
    Context,
    Added,
    Removed,
}

/// <summary>One line of a <see cref="LineDiff"/> — its <see cref="Kind"/> and the line text.</summary>
public sealed record DiffLine(DiffLineKind Kind, string Text);

/// <summary>
/// Line-level diff of two texts via a longest-common-subsequence backtrace — the same algorithm the
/// web renders (<c>web/src/app/prompts/diff.ts</c>), reproduced here so the backport artifact (1.20)
/// can embed a "diff vs Current" without a diff library. A changed line surfaces as a removal of the
/// old line followed by an addition of the new one. Deliberate TS↔C# duplication (different runtimes,
/// ~30 lines) over a shared package.
/// </summary>
public static class LineDiff
{
    public static IReadOnlyList<DiffLine> Compute(string before, string after)
    {
        var a = before.Split('\n');
        var b = after.Split('\n');
        var n = a.Length;
        var m = b.Length;

        // lcs[i][j] = length of the LCS of a[i..] and b[j..].
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var outLines = new List<DiffLine>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                outLines.Add(new DiffLine(DiffLineKind.Context, a[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                outLines.Add(new DiffLine(DiffLineKind.Removed, a[x]));
                x++;
            }
            else
            {
                outLines.Add(new DiffLine(DiffLineKind.Added, b[y]));
                y++;
            }
        }

        while (x < n)
            outLines.Add(new DiffLine(DiffLineKind.Removed, a[x++]));
        while (y < m)
            outLines.Add(new DiffLine(DiffLineKind.Added, b[y++]));

        return outLines;
    }
}
