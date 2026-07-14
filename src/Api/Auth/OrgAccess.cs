using Application.Ports;

namespace Api.Auth;

/// <summary>
/// The outcome of a resolve-then-check access question for a by-id resource (4.1). The endpoint
/// maps it to a status code: a missing entity stays <c>404</c>, an entity the caller can't reach
/// becomes <c>403</c>, and an accessible one proceeds. Keeping "not found" and "forbidden" distinct
/// avoids leaking existence through the status code while preserving the existing 404 semantics.
/// </summary>
public enum ResourceAccess
{
    NotFound,
    Forbidden,
    Allowed,
}

/// <summary>
/// The request-scoped authorization helper (4.1). The organization is the permission boundary, so
/// every question reduces to "is the current user a member of the org that owns this resource?".
/// It resolves a resource to its owning organization — directly for prompts/folders, and via the
/// owning prompt for datasets and eval-runs — then checks membership through <see cref="IUserDirectory"/>.
/// Registered scoped; injected into the data endpoints.
/// </summary>
public sealed class OrgAccess(
    ICurrentUser current,
    IUserDirectory users,
    IPromptRepository prompts,
    IDatasetRepository datasets,
    IEvalRunRepository evalRuns,
    IFolderRepository folders)
{
    /// <summary>True when the current user is a member of <paramref name="orgId"/> — the base check.</summary>
    public async Task<bool> CanAccessOrgAsync(Guid orgId, CancellationToken ct)
        => current.UserId is { } uid && await users.IsMemberAsync(uid, orgId, ct);

    /// <summary>Resolves a prompt to its org (O(1) via <c>Prompt.OrganizationId</c>) and checks access.</summary>
    public async Task<ResourceAccess> CanAccessPromptAsync(Guid promptId, CancellationToken ct)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        return prompt is null ? ResourceAccess.NotFound : await ResolveAsync(prompt.OrganizationId, ct);
    }

    /// <summary>Resolves a folder to its org and checks access.</summary>
    public async Task<ResourceAccess> CanAccessFolderAsync(Guid folderId, CancellationToken ct)
    {
        var folder = await folders.GetByIdAsync(folderId, ct);
        return folder is null ? ResourceAccess.NotFound : await ResolveAsync(folder.OrganizationId, ct);
    }

    /// <summary>Resolves a dataset via its owning prompt (dataset → prompt → org) and checks access.</summary>
    public async Task<ResourceAccess> CanAccessDatasetAsync(Guid datasetId, CancellationToken ct)
    {
        var dataset = await datasets.GetByIdAsync(datasetId, ct);
        return dataset is null ? ResourceAccess.NotFound : await CanAccessPromptAsync(dataset.PromptId, ct);
    }

    /// <summary>Resolves an eval run via its prompt (evalRun → prompt → org) and checks access.</summary>
    public async Task<ResourceAccess> CanAccessEvalRunAsync(Guid evalRunId, CancellationToken ct)
    {
        var run = await evalRuns.GetByIdAsync(evalRunId, ct);
        return run is null ? ResourceAccess.NotFound : await CanAccessPromptAsync(run.PromptId, ct);
    }

    /// <summary>The set of org ids the current user may access — the list endpoints filter on this.</summary>
    public async Task<IReadOnlySet<Guid>> AccessibleOrgIdsAsync(CancellationToken ct)
        => current.UserId is { } uid
            ? (await users.GetAccessibleOrganizationIdsAsync(uid, ct)).ToHashSet()
            : new HashSet<Guid>();

    private async Task<ResourceAccess> ResolveAsync(Guid orgId, CancellationToken ct)
        => await CanAccessOrgAsync(orgId, ct) ? ResourceAccess.Allowed : ResourceAccess.Forbidden;
}

/// <summary>Maps a <see cref="ResourceAccess"/> to the failing result, or null when access is allowed.</summary>
public static class ResourceAccessExtensions
{
    public static IResult? ToProblem(this ResourceAccess access) => access switch
    {
        ResourceAccess.NotFound => Results.NotFound(),
        ResourceAccess.Forbidden => Results.Forbid(),
        _ => null,
    };
}
