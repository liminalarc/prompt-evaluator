using Api.Auth;
using Application.Folders;
using Application.Ports;
using Application.Prompts;

namespace Api.Prompts;

public static class PromptEndpoints
{
    public static IEndpointRouteBuilder MapPromptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prompts").RequireAuthorization();

        // Filtered to the caller's accessible orgs (4.1).
        group.MapGet("/", async (IPromptRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var accessible = await access.AccessibleOrgIdsAsync(ct);
            var prompts = await repository.ListAsync(ct);
            return Results.Ok(prompts.Where(p => accessible.Contains(p.OrganizationId)).Select(PromptSummaryResponse.From));
        });

        group.MapGet("/{id:guid}", async (Guid id, IPromptRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            var prompt = await repository.GetByIdAsync(id, ct);
            return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
        });

        // Prompts belong to an organization (1.9): created and browsed under their org.
        app.MapGet("/api/organizations/{orgId:guid}/prompts",
            async (Guid orgId, IPromptRepository repository, OrgAccess access, CancellationToken ct) =>
            {
                if (!await access.CanAccessOrgAsync(orgId, ct))
                    return Results.Forbid();
                var prompts = await repository.ListByOrganizationAsync(orgId, ct);
                return Results.Ok(prompts.Select(PromptSummaryResponse.From));
            }).RequireAuthorization();

        app.MapPost("/api/organizations/{orgId:guid}/prompts",
            async (Guid orgId, CreatePromptRequest request, CreatePromptHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if (!await access.CanAccessOrgAsync(orgId, ct))
                    return Results.Forbid();
                try
                {
                    var prompt = await handler.HandleAsync(orgId, request.Name, request.Description, ct);
                    return prompt is null
                        ? Results.NotFound(new { error = "Organization not found." })
                        : Results.Created($"/api/prompts/{prompt.Id}", PromptResponse.From(prompt));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }).RequireAuthorization();

        // Move a prompt into a folder (1.7), or unfile it to the root when FolderId is null.
        group.MapPost("/{id:guid}/move",
            async (Guid id, MovePromptRequest request, MovePromptHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                try
                {
                    var prompt = await handler.HandleAsync(id, request.FolderId, ct);
                    return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        // Deletes a prompt and everything it owns (1.10): versions, datasets, and those datasets'
        // fixtures/scorer-configs/eval-runs/scores. Org-scoped — a missing prompt is 404, a
        // non-member is 403 (via OrgAccess); a delete the caller may perform returns 204.
        group.MapDelete("/{id:guid}", async (Guid id, IPromptRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            await repository.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/versions",
            async (Guid id, AddPromptVersionRequest request, AddPromptVersionHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                try
                {
                    var prompt = await handler.HandleAsync(
                        id, request.Content, request.TargetModel, request.Label, request.SourceApp, ct);
                    return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        return app;
    }
}
