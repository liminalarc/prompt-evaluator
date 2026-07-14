using Api.Auth;
using Api.Prompts;
using Application.Folders;
using Application.Ports;

namespace Api.Folders;

public static class FolderEndpoints
{
    public static IEndpointRouteBuilder MapFolderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/folders").RequireAuthorization();

        // An organization's folder tree as a flat list — the client assembles it via ParentId (1.9).
        app.MapGet("/api/organizations/{orgId:guid}/folders",
            async (Guid orgId, IFolderRepository repository, OrgAccess access, CancellationToken ct) =>
            {
                if (!await access.CanAccessOrgAsync(orgId, ct))
                    return Results.Forbid();
                var folders = await repository.ListByOrganizationAsync(orgId, ct);
                return Results.Ok(folders.Select(FolderResponse.From));
            }).RequireAuthorization();

        app.MapPost("/api/organizations/{orgId:guid}/folders",
            async (Guid orgId, CreateFolderRequest request, CreateFolderHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if (!await access.CanAccessOrgAsync(orgId, ct))
                    return Results.Forbid();
                try
                {
                    var folder = await handler.HandleAsync(orgId, request.Name, request.ParentId, ct);
                    return folder is null
                        ? Results.NotFound(new { error = "Organization or parent folder not found." })
                        : Results.Created($"/api/folders/{folder.Id}", FolderResponse.From(folder));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }).RequireAuthorization();

        // The prompts filed directly in a folder.
        group.MapGet("/{id:guid}/prompts", async (Guid id, IPromptRepository prompts, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessFolderAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            var filed = await prompts.ListByFolderAsync(id, ct);
            return Results.Ok(filed.Select(PromptSummaryResponse.From));
        });

        // The unfiled prompts — the contents of the root, filtered to the caller's accessible orgs (4.1).
        group.MapGet("/root/prompts", async (IPromptRepository prompts, OrgAccess access, CancellationToken ct) =>
        {
            var accessible = await access.AccessibleOrgIdsAsync(ct);
            var unfiled = await prompts.ListByFolderAsync(null, ct);
            return Results.Ok(unfiled.Where(p => accessible.Contains(p.OrganizationId)).Select(PromptSummaryResponse.From));
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameFolderRequest request, RenameFolderHandler handler, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessFolderAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            try
            {
                var folder = await handler.HandleAsync(id, request.Name, ct);
                return folder is null ? Results.NotFound() : Results.Ok(FolderResponse.From(folder));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/move", async (Guid id, MoveFolderRequest request, MoveFolderHandler handler, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessFolderAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            try
            {
                var folder = await handler.HandleAsync(id, request.ParentId, ct);
                return folder is null ? Results.NotFound() : Results.Ok(FolderResponse.From(folder));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
