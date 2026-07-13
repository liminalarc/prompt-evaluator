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
        var group = app.MapGroup("/api/datasets");

        group.MapGet("/", async (IDatasetRepository repository, CancellationToken ct) =>
        {
            var datasets = await repository.ListAsync(ct);
            return Results.Ok(datasets.Select(DatasetSummaryResponse.From));
        });

        group.MapGet("/{id:guid}", async (Guid id, IDatasetRepository repository, CancellationToken ct) =>
        {
            var dataset = await repository.GetByIdAsync(id, ct);
            return dataset is null ? Results.NotFound() : Results.Ok(DatasetResponse.From(dataset));
        });

        // Datasets live with a prompt (1.7): they are created and browsed under their owning prompt.
        app.MapGet("/api/prompts/{promptId:guid}/datasets",
            async (Guid promptId, IDatasetRepository repository, CancellationToken ct) =>
            {
                var datasets = await repository.ListByPromptAsync(promptId, ct);
                return Results.Ok(datasets.Select(DatasetSummaryResponse.From));
            });

        app.MapPost("/api/prompts/{promptId:guid}/datasets",
            async (Guid promptId, CreateDatasetUnderPromptRequest request, CreateDatasetHandler handler, CancellationToken ct) =>
            {
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
            });

        group.MapPost("/{id:guid}/fixtures/capture",
            async (Guid id, CaptureFixturesRequest request, CaptureFixturesHandler handler, CancellationToken ct) =>
            {
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
            async (Guid id, GenerateFixturesRequest request, GenerateSyntheticFixturesHandler handler, CancellationToken ct) =>
            {
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
