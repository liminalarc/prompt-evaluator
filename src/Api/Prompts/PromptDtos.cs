using Domain;

namespace Api.Prompts;

public sealed record CreatePromptRequest(string Name, string? Description);

public sealed record AddPromptVersionRequest(
    string Content, string TargetModel, string? Label, string? SourceApp);

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
    string Name,
    string? Description,
    IReadOnlyList<PromptVersionResponse> Versions)
{
    public static PromptResponse From(Prompt p) =>
        new(p.Id, p.Name, p.Description, p.Versions.Select(PromptVersionResponse.From).ToList());
}

/// <summary>Lightweight projection for the browse/list view — no version bodies.</summary>
public sealed record PromptSummaryResponse(
    Guid Id, string Name, string? Description, int VersionCount, string? LatestTargetModel)
{
    public static PromptSummaryResponse From(Prompt p) =>
        new(p.Id, p.Name, p.Description, p.Versions.Count, p.Versions.LastOrDefault()?.TargetModel);
}
