using Application.Datasets;
using Application.Ports;

namespace Api.Datasets;

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

        group.MapPost("/", async (CreateDatasetRequest request, CreateDatasetHandler handler, CancellationToken ct) =>
        {
            try
            {
                var dataset = await handler.HandleAsync(request.Name, request.Description, ct);
                return Results.Created($"/api/datasets/{dataset.Id}", DatasetResponse.From(dataset));
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

        return app;
    }
}
