using Api.Auth;
using Application.Analytics;
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

        // Edit a version's editable metadata — its label only (U3). Content + target model are
        // immutable (run identity), so they are not accepted here.
        group.MapPatch("/{id:guid}/versions/{versionId:guid}",
            async (Guid id, Guid versionId, EditPromptVersionRequest request, EditPromptVersionHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var prompt = await handler.HandleAsync(id, versionId, request.Label, ct);
                return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
            });

        // The derived per-version lifecycle status (1.16): Current / Backport-eligible / Regressed.
        group.MapGet("/{id:guid}/version-status",
            async (Guid id, VersionStatusHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var status = await handler.HandleAsync(id, ct);
                return status is null ? Results.NotFound() : Results.Ok(PromptVersionStatusResponse.From(status));
            });

        // Mark a version "Current in source" (1.16) — also the mark-as-backported action (move Current
        // to a shipped, higher-scoring version). Returns the recomputed status so badges update at once.
        group.MapPost("/{id:guid}/versions/{versionId:guid}/set-current",
            async (Guid id, Guid versionId, SetCurrentVersionRequest request, SetCurrentVersionHandler handler,
                VersionStatusHandler status, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var outcome = await handler.HandleAsync(id, versionId, request.CommitSha, ct);
                return outcome switch
                {
                    SetCurrentVersionHandler.Outcome.PromptNotFound => Results.NotFound(),
                    SetCurrentVersionHandler.Outcome.VersionNotFound =>
                        Results.NotFound(new { error = "That version is not part of this prompt." }),
                    _ => Results.Ok(PromptVersionStatusResponse.From((await status.HandleAsync(id, ct))!)),
                };
            });

        // The generated backport artifact for the prompt's single backport target (1.20): the
        // ready-to-apply prompt + downloadable markdown. 404 when the prompt has no target (nothing
        // to ship) — the UI only offers "Prepare backport" when a target exists.
        group.MapGet("/{id:guid}/backport-artifact",
            async (Guid id, BackportArtifactHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var artifact = await handler.HandleAsync(id, ct);
                return artifact is null ? Results.NotFound() : Results.Ok(BackportArtifactResponse.From(artifact));
            });

        return app;
    }
}
