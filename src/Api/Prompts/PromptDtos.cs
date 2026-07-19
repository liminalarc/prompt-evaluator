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
    Guid VersionId, int VersionNumber, string? Label, bool IsCurrent, bool BackportEligible, bool Regressed)
{
    public static VersionStatusResponse From(VersionStatus s) =>
        new(s.VersionId, s.VersionNumber, s.Label, s.IsCurrent, s.BackportEligible, s.Regressed);
}

/// <summary>A prompt's per-version status set + its Current-in-source pointer (1.16).</summary>
public sealed record PromptVersionStatusResponse(
    Guid PromptId, Guid? CurrentVersionId, IReadOnlyList<VersionStatusResponse> Versions)
{
    public static PromptVersionStatusResponse From(PromptVersionStatus s) =>
        new(s.PromptId, s.CurrentVersionId, s.Versions.Select(VersionStatusResponse.From).ToList());
}

/// <summary>Lightweight projection for the browse/list view — no version bodies.</summary>
public sealed record PromptSummaryResponse(
    Guid Id, Guid? FolderId, string Name, string? Description, int VersionCount, string? LatestTargetModel)
{
    public static PromptSummaryResponse From(Prompt p) =>
        new(p.Id, p.FolderId, p.Name, p.Description, p.Versions.Count, p.Versions.LastOrDefault()?.TargetModel);
}

public sealed record MovePromptRequest(Guid? FolderId);
