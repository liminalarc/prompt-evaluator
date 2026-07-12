using Application.Datasets;
using Application.EvalRuns;
using Application.Ports;
using Domain;

namespace Api.EvalRuns;

public static class EvalHarnessEndpoints
{
    public static IEndpointRouteBuilder MapEvalHarnessEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- Scorer configuration (per dataset) ----

        app.MapGet("/api/datasets/{id:guid}/scorers",
            async (Guid id, ConfigureDatasetScorersHandler handler, CancellationToken ct) =>
            {
                var configs = await handler.ListAsync(id, ct);
                return Results.Ok(configs.Select(ScorerConfigResponse.From));
            });

        app.MapPost("/api/datasets/{id:guid}/scorers",
            async (Guid id, ConfigureScorerRequest request, ConfigureDatasetScorersHandler handler, CancellationToken ct) =>
            {
                if (!Enum.TryParse<ScorerKind>(request.Kind, ignoreCase: true, out var kind))
                    return Results.BadRequest(new { error = $"Unknown scorer kind '{request.Kind}'." });

                ScorerDescriptor descriptor;
                try
                {
                    descriptor = kind == ScorerKind.LlmJudge
                        ? ScorerDescriptor.LlmJudge(request.Config ?? "", request.JudgeModel ?? "")
                        : ScorerDescriptor.Deterministic(kind, request.Config);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                var config = await handler.HandleAsync(id, descriptor, ct);
                return config is null
                    ? Results.NotFound()
                    : Results.Created($"/api/datasets/{id}/scorers", ScorerConfigResponse.From(config));
            });

        // ---- Eval runs ----

        app.MapPost("/api/datasets/{id:guid}/eval-runs",
            async (Guid id, CreateEvalRunRequest request, RunEvaluationHandler handler, CancellationToken ct) =>
            {
                var run = await handler.HandleAsync(request.PromptId, request.PromptVersionId, id, ct);
                return run is null
                    ? Results.NotFound()
                    : Results.Created($"/api/eval-runs/{run.Id}", EvalRunResponse.From(run));
            });

        app.MapGet("/api/datasets/{id:guid}/eval-runs",
            async (Guid id, IEvalRunRepository repository, CancellationToken ct) =>
            {
                var runs = await repository.ListByDatasetAsync(id, ct);
                return Results.Ok(runs.Select(EvalRunSummaryResponse.From));
            });

        app.MapGet("/api/eval-runs/{id:guid}",
            async (Guid id, IEvalRunRepository repository, CancellationToken ct) =>
            {
                var run = await repository.GetByIdAsync(id, ct);
                return run is null ? Results.NotFound() : Results.Ok(EvalRunResponse.From(run));
            });

        return app;
    }
}
