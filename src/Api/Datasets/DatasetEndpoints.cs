using Api.Auth;
using Application.Datasets;
using Application.Ports;

namespace Api.Datasets;

file static class GenerationDefaults
{
    public const int Count = 5;
}

public static class DatasetEndpoints
{
    public static IEndpointRouteBuilder MapDatasetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/datasets").RequireAuthorization();

        // Filtered to datasets under a prompt in one of the caller's accessible orgs (4.1).
        group.MapGet("/", async (IDatasetRepository repository, IPromptRepository prompts, OrgAccess access, CancellationToken ct) =>
        {
            var accessible = await access.AccessibleOrgIdsAsync(ct);
            var accessiblePromptIds = (await prompts.ListAsync(ct))
                .Where(p => accessible.Contains(p.OrganizationId))
                .Select(p => p.Id)
                .ToHashSet();
            var datasets = await repository.ListAsync(ct);
            return Results.Ok(datasets.Where(d => accessiblePromptIds.Contains(d.PromptId)).Select(DatasetSummaryResponse.From));
        });

        group.MapGet("/{id:guid}", async (Guid id, IDatasetRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            var dataset = await repository.GetByIdAsync(id, ct);
            return dataset is null ? Results.NotFound() : Results.Ok(DatasetResponse.From(dataset));
        });

        // Datasets live with a prompt (1.7): they are created and browsed under their owning prompt.
        app.MapGet("/api/prompts/{promptId:guid}/datasets",
            async (Guid promptId, IDatasetRepository repository, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(promptId, ct)).ToProblem() is { } problem)
                    return problem;
                var datasets = await repository.ListByPromptAsync(promptId, ct);
                return Results.Ok(datasets.Select(DatasetSummaryResponse.From));
            }).RequireAuthorization();

        app.MapPost("/api/prompts/{promptId:guid}/datasets",
            async (Guid promptId, CreateDatasetUnderPromptRequest request, CreateDatasetHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessPromptAsync(promptId, ct)).ToProblem() is { } problem)
                    return problem;
                try
                {
                    var dataset = await handler.HandleAsync(promptId, request.Name, request.Description, ct);
                    return dataset is null
                        ? Results.NotFound(new { error = "Prompt not found." })
                        : Results.Created($"/api/datasets/{dataset.Id}", DatasetResponse.From(dataset));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }).RequireAuthorization();

        // Deletes a dataset and everything scoped to it (1.10): fixtures, scorer-configs, eval-runs,
        // scores. Org-scoped via the owning prompt — missing is 404, a non-member is 403.
        group.MapDelete("/{id:guid}", async (Guid id, IDatasetRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                return problem;
            await repository.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/fixtures/capture",
            async (Guid id, CaptureFixturesRequest request, CaptureFixturesHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var tuples = (request.Tuples ?? new())
                    .Select(t => new CapturedTuple(t.PromptInput, t.SlmOutput, t.DownstreamResult))
                    .ToList();
                try
                {
                    var dataset = await handler.HandleAsync(id, tuples, ct);
                    return dataset is null ? Results.NotFound() : Results.Ok(DatasetResponse.From(dataset));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        group.MapPost("/{id:guid}/fixtures/generate",
            async (Guid id, GenerateFixturesRequest request, GenerateSyntheticFixturesHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var g = request.Guidance;
                var guidance = new GenerationGuidanceData(g?.CoverageGoals, g?.EdgeCases, g?.Constraints);
                var count = request.Count is int c && c > 0 ? c : GenerationDefaults.Count;
                try
                {
                    var dataset = await handler.HandleAsync(id, guidance, count, ct);
                    return dataset is null ? Results.NotFound() : Results.Ok(DatasetResponse.From(dataset));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        return app;
    }
}
