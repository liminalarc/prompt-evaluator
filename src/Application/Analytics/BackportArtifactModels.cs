namespace Application.Analytics;

/// <summary>
/// One row of the backport artifact's score-delta summary (1.20): how the <b>target</b> scores against
/// <b>Current</b> for a single scorer on a single dataset. <see cref="Delta"/> is
/// <c>Target − Current</c> (positive = the target improves). One row per (dataset × scorer) the two
/// versions share — faithful to how eligibility spans every dataset, not a single one.
/// </summary>
public sealed record BackportScoreDelta(
    string DatasetName,
    string ScorerLabel,
    double CurrentMean,
    double TargetMean,
    double Delta);

/// <summary>
/// The generated backport artifact for a prompt's single backport target (1.20) — everything a
/// maintainer needs to hand-apply the ship in the source app. LitmusAI produces this; it never writes
/// to a source repo (signal-only, per [[1.16]]; wired-in automation is [[3.1]]).
///
/// <see cref="Content"/> is the target version's exact body (the copy-to-clipboard "exact prompt");
/// <see cref="Markdown"/> is the full downloadable <c>.md</c> (name, <c>Current vN → target vM</c>,
/// target model, Current's SHA, the full new content, the diff vs Current, the per-scorer score-delta
/// summary, and an apply checklist). <see cref="Diff"/> is the structured diff vs Current's content.
/// </summary>
public sealed record BackportArtifact(
    Guid PromptId,
    string PromptName,
    int CurrentVersionNumber,
    string? CurrentVersionSha,
    int TargetVersionNumber,
    string TargetModel,
    string Content,
    IReadOnlyList<DiffLine> Diff,
    IReadOnlyList<BackportScoreDelta> ScoreDeltas,
    string Markdown,
    string FileName);
