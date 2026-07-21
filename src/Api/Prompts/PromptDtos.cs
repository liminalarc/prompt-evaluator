using Application.Analytics;
using Domain;

namespace Api.Prompts;

public sealed record CreatePromptRequest(string Name, string? Description);

public sealed record AddPromptVersionRequest(
    string Content, string TargetModel, string? Label, string? SourceApp);

/// <summary>Editable version metadata — the label only; content/target model are immutable.</summary>
public sealed record EditPromptVersionRequest(string? Label);

public sealed record PromptVersionResponse(
    Guid Id,
    int VersionNumber,
    string Content,
    string TargetModel,
    string? Label,
    string? SourceApp,
    DateTimeOffset CreatedAt)
{
    public static PromptVersionResponse From(PromptVersion v) =>
        new(v.Id, v.VersionNumber, v.Content, v.TargetModel, v.Label, v.SourceApp, v.CreatedAt);
}

public sealed record PromptResponse(
    Guid Id,
    Guid? FolderId,
    string Name,
    string? Description,
    IReadOnlyList<PromptVersionResponse> Versions,
    Guid? CurrentVersionId,
    string? CurrentVersionSha,
    DateTimeOffset? CurrentVersionSetAt)
{
    public static PromptResponse From(Prompt p) =>
        new(p.Id, p.FolderId, p.Name, p.Description,
            p.Versions.Select(PromptVersionResponse.From).ToList(),
            p.CurrentVersionId, p.CurrentVersionSha, p.CurrentVersionSetAt);
}

/// <summary>Request to mark a version Current in source (1.16); optional commit SHA of what shipped.</summary>
public sealed record SetCurrentVersionRequest(string? CommitSha);

/// <summary>One version's derived lifecycle status (1.16) — the badges the UI renders.</summary>
public sealed record VersionStatusResponse(
    Guid VersionId, int VersionNumber, string? Label,
    bool IsCurrent, bool BackportEligible, bool IsBackportTarget, bool Regressed)
{
    public static VersionStatusResponse From(VersionStatus s) =>
        new(s.VersionId, s.VersionNumber, s.Label, s.IsCurrent, s.BackportEligible, s.IsBackportTarget, s.Regressed);
}

/// <summary>A prompt's per-version status + its Current-in-source pointer and single backport target (1.16).</summary>
public sealed record PromptVersionStatusResponse(
    Guid PromptId, Guid? CurrentVersionId, Guid? BackportTargetVersionId,
    IReadOnlyList<VersionStatusResponse> Versions, int CrossModelVersionsExcluded)
{
    public static PromptVersionStatusResponse From(PromptVersionStatus s) =>
        new(s.PromptId, s.CurrentVersionId, s.BackportTargetVersionId,
            s.Versions.Select(VersionStatusResponse.From).ToList(), s.CrossModelVersionsExcluded);
}

/// <summary>One line of the backport artifact's diff vs Current (1.20). Kind is <c>context</c> /
/// <c>added</c> / <c>removed</c> (lower-cased to match the web diff model).</summary>
public sealed record BackportDiffLineResponse(string Kind, string Text)
{
    public static BackportDiffLineResponse From(DiffLine l) =>
        new(l.Kind.ToString().ToLowerInvariant(), l.Text);
}

/// <summary>One per-scorer score-delta row (target vs Current) in the backport artifact (1.20).</summary>
public sealed record BackportScoreDeltaResponse(
    string DatasetName, string ScorerLabel, double CurrentMean, double TargetMean, double Delta)
{
    public static BackportScoreDeltaResponse From(BackportScoreDelta d) =>
        new(d.DatasetName, d.ScorerLabel, d.CurrentMean, d.TargetMean, d.Delta);
}

/// <summary>The generated backport artifact for a prompt's single backport target (1.20):
/// <see cref="Content"/> is the copy-to-clipboard exact prompt; <see cref="Markdown"/> is the
/// downloadable <c>.md</c>; <see cref="Diff"/>/<see cref="ScoreDeltas"/> are the structured
/// building blocks. LitmusAI signals only — this is an artifact, never a source-repo write.</summary>
public sealed record BackportArtifactResponse(
    Guid PromptId,
    string PromptName,
    int CurrentVersionNumber,
    string? CurrentVersionSha,
    int TargetVersionNumber,
    string TargetModel,
    string Content,
    IReadOnlyList<BackportDiffLineResponse> Diff,
    IReadOnlyList<BackportScoreDeltaResponse> ScoreDeltas,
    string Markdown,
    string FileName)
{
    public static BackportArtifactResponse From(BackportArtifact a) =>
        new(a.PromptId, a.PromptName, a.CurrentVersionNumber, a.CurrentVersionSha,
            a.TargetVersionNumber, a.TargetModel, a.Content,
            a.Diff.Select(BackportDiffLineResponse.From).ToList(),
            a.ScoreDeltas.Select(BackportScoreDeltaResponse.From).ToList(),
            a.Markdown, a.FileName);
}

/// <summary>Lightweight projection for the browse/list view — no version bodies.</summary>
public sealed record PromptSummaryResponse(
    Guid Id, Guid? FolderId, string Name, string? Description, int VersionCount, string? LatestTargetModel)
{
    public static PromptSummaryResponse From(Prompt p) =>
        new(p.Id, p.FolderId, p.Name, p.Description, p.Versions.Count, p.Versions.LastOrDefault()?.TargetModel);
}

public sealed record MovePromptRequest(Guid? FolderId);
