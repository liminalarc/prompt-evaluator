using Api.Prompts;
using Application.Folders;
using Application.Ports;

namespace Api.Folders;

public static class FolderEndpoints
{
    public static IEndpointRouteBuilder MapFolderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/folders");

        // The whole tree as a flat list — the client assembles it via ParentId.
        group.MapGet("/", async (IFolderRepository repository, CancellationToken ct) =>
        {
            var folders = await repository.ListAsync(ct);
            return Results.Ok(folders.Select(FolderResponse.From));
        });

        // The prompts filed directly in a folder.
        group.MapGet("/{id:guid}/prompts", async (Guid id, IPromptRepository prompts, CancellationToken ct) =>
        {
            var filed = await prompts.ListByFolderAsync(id, ct);
            return Results.Ok(filed.Select(PromptSummaryResponse.From));
        });

        // The unfiled prompts — the contents of the root.
        group.MapGet("/root/prompts", async (IPromptRepository prompts, CancellationToken ct) =>
        {
            var unfiled = await prompts.ListByFolderAsync(null, ct);
            return Results.Ok(unfiled.Select(PromptSummaryResponse.From));
        });

        group.MapPost("/", async (CreateFolderRequest request, CreateFolderHandler handler, CancellationToken ct) =>
        {
            try
            {
                var folder = await handler.HandleAsync(request.Name, request.ParentId, ct);
                return folder is null
                    ? Results.NotFound(new { error = "Parent folder not found." })
                    : Results.Created($"/api/folders/{folder.Id}", FolderResponse.From(folder));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameFolderRequest request, RenameFolderHandler handler, CancellationToken ct) =>
        {
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

        group.MapPost("/{id:guid}/move", async (Guid id, MoveFolderRequest request, MoveFolderHandler handler, CancellationToken ct) =>
        {
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
