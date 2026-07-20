using System.Globalization;
using System.Text;
using Application.Ports;

namespace Application.Analytics;

/// <summary>
/// Assembles the backport artifact (1.20) for a prompt's single backport target — the ready-to-apply
/// bridge on top of [[1.16]]'s "which version to ship" signal. Resolves the target + Current via
/// <see cref="VersionStatusHandler"/>, diffs the target's content against Current's
/// (<see cref="LineDiff"/>), summarizes the per-scorer score deltas over every dataset the two share
/// (reusing <see cref="ComparisonAnalyticsHandler"/>), and renders both the exact-prompt body and a
/// downloadable markdown. Read-only — LitmusAI signals; it never writes to a source repo.
/// </summary>
public sealed class BackportArtifactHandler(
    IPromptRepository prompts,
    IDatasetRepository datasets,
    VersionStatusHandler status,
    ComparisonAnalyticsHandler comparison)
{
    /// <summary>Null when the prompt does not exist or has no backport target (Api → 404).</summary>
    public async Task<BackportArtifact?> HandleAsync(Guid promptId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        var versionStatus = await status.HandleAsync(promptId, ct);
        if (versionStatus?.BackportTargetVersionId is not { } targetId ||
            versionStatus.CurrentVersionId is not { } currentId)
            return null; // no target (target requires a Current to beat)

        var current = prompt.Versions.FirstOrDefault(v => v.Id == currentId);
        var target = prompt.Versions.FirstOrDefault(v => v.Id == targetId);
        if (current is null || target is null)
            return null;

        var diff = LineDiff.Compute(current.Content, target.Content);

        // Per-scorer score deltas over every dataset the two versions share (target vs Current).
        var deltas = new List<BackportScoreDelta>();
        foreach (var dataset in await datasets.ListByPromptAsync(promptId, ct))
        {
            var cmp = await comparison.HandleAsync(promptId, dataset.Id, currentId, targetId, ct);
            if (cmp is null)
                continue;
            foreach (var sc in cmp.Scorers)
            {
                if (sc.FromMean is not { } from || sc.ToMean is not { } to)
                    continue; // only scorers both versions ran on this dataset are comparable
                deltas.Add(new BackportScoreDelta(dataset.Name, ScorerLabel(sc.Scorer), from, to, to - from));
            }
        }

        var markdown = BuildMarkdown(prompt.Name, current.VersionNumber, target.VersionNumber,
            target.TargetModel, prompt.CurrentVersionSha, target.Content, diff, deltas);
        var fileName = BuildFileName(prompt.Name, current.VersionNumber, target.VersionNumber);

        return new BackportArtifact(
            promptId, prompt.Name, current.VersionNumber, prompt.CurrentVersionSha,
            target.VersionNumber, target.TargetModel, target.Content, diff, deltas, markdown, fileName);
    }

    private static string ScorerLabel(ScorerRef scorer)
        => scorer.JudgeModel is { Length: > 0 } model ? $"{scorer.Kind} ({model})" : scorer.Kind.ToString();

    private static string BuildMarkdown(
        string promptName, int currentVersion, int targetVersion, string targetModel,
        string? currentSha, string targetContent, IReadOnlyList<DiffLine> diff,
        IReadOnlyList<BackportScoreDelta> deltas)
    {
        var sb = new StringBuilder();
        sb.Append("# Backport: ").AppendLine(promptName);
        sb.AppendLine();
        sb.Append("**Ship:** Current v").Append(currentVersion)
            .Append(" → target **v").Append(targetVersion).AppendLine("**");
        sb.Append("**Target model:** `").Append(targetModel).AppendLine("`");
        sb.Append("**Current commit:** ")
            .AppendLine(currentSha is { Length: > 0 } sha ? $"`{sha}`" : "_not recorded_");
        sb.AppendLine();
        sb.AppendLine("> LitmusAI signals only — apply this in the source app's own process, then");
        sb.Append("> return here and **Mark backported → v").Append(targetVersion).AppendLine("**.");
        sb.AppendLine();

        sb.Append("## New prompt content (v").Append(targetVersion).AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("```text");
        sb.AppendLine(targetContent);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.Append("## Diff vs Current (v").Append(currentVersion)
            .Append(" → v").Append(targetVersion).AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("```diff");
        foreach (var line in diff)
        {
            var marker = line.Kind switch
            {
                DiffLineKind.Added => "+",
                DiffLineKind.Removed => "-",
                _ => " ",
            };
            sb.Append(marker).Append(' ').AppendLine(line.Text);
        }
        sb.AppendLine("```");
        sb.AppendLine();

        sb.Append("## Score delta (target v").Append(targetVersion)
            .Append(" vs Current v").Append(currentVersion).AppendLine(")");
        sb.AppendLine();
        if (deltas.Count == 0)
        {
            sb.AppendLine("_No shared scored runs to compare._");
        }
        else
        {
            sb.AppendLine("| Dataset | Scorer | Current | Target | Δ |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var d in deltas)
                sb.Append("| ").Append(d.DatasetName)
                    .Append(" | ").Append(d.ScorerLabel)
                    .Append(" | ").Append(Num(d.CurrentMean))
                    .Append(" | ").Append(Num(d.TargetMean))
                    .Append(" | ").Append(Signed(d.Delta))
                    .AppendLine(" |");
        }
        sb.AppendLine();

        sb.AppendLine("## Apply checklist");
        sb.AppendLine();
        sb.Append("- [ ] Open the source app's prompt for **").Append(promptName).AppendLine("**");
        sb.Append("- [ ] Replace its content with v").Append(targetVersion).AppendLine(" (above)");
        sb.Append("- [ ] Confirm the target model is `").Append(targetModel).AppendLine("`");
        sb.AppendLine("- [ ] Commit in the source repo and note the SHA");
        sb.Append("- [ ] Back in LitmusAI, **Mark backported → v").Append(targetVersion).AppendLine("**");

        return sb.ToString();
    }

    private static string Num(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Signed(double value)
        => (value >= 0 ? "+" : "") + value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string BuildFileName(string promptName, int currentVersion, int targetVersion)
    {
        var slug = new string(promptName.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        slug = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length == 0)
            slug = "prompt";
        return $"backport-{slug}-v{currentVersion}-to-v{targetVersion}.md";
    }
}
