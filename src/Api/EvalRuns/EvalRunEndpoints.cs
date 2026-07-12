using Application.EvalRuns;
using Application.Ports;

namespace Api.EvalRuns;

public static class EvalRunEndpoints
{
    public static IEndpointRouteBuilder MapEvalRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/eval-runs");

        group.MapPost("/", async (CreateEvalRunRequest request, CreateEvalRunHandler handler, CancellationToken ct) =>
        {
            var run = await handler.HandleAsync(request.Prompt, ct);
            var response = EvalRunResponse.From(run);
            return Results.Created($"/api/eval-runs/{run.Id}", response);
        });

        group.MapGet("/{id:guid}", async (Guid id, IEvalRunRepository repository, CancellationToken ct) =>
        {
            var run = await repository.GetByIdAsync(id, ct);
            return run is null ? Results.NotFound() : Results.Ok(EvalRunResponse.From(run));
        });

        return app;
    }
}
