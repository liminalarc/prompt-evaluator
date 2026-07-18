using Api.Auth;
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
            async (Guid id, ConfigureDatasetScorersHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var configs = await handler.ListAsync(id, ct);
                return Results.Ok(configs.Select(ScorerConfigResponse.From));
            }).RequireAuthorization();

        app.MapPost("/api/datasets/{id:guid}/scorers",
            async (Guid id, ConfigureScorerRequest request, ConfigureDatasetScorersHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
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
            }).RequireAuthorization();

        // ---- Eval runs ----

        app.MapPost("/api/datasets/{id:guid}/eval-runs",
            async (Guid id, CreateEvalRunRequest request, RunEvaluationHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                try
                {
                    var run = await handler.HandleAsync(request.PromptId, request.PromptVersionId, id, ct);
                    return run is null
                        ? Results.NotFound()
                        : Results.Created($"/api/eval-runs/{run.Id}", EvalRunResponse.From(run));
                }
                catch (EvalRunnerException ex)
                {
                    // The run reached the eval-runner but it failed (e.g. a provider with no
                    // credentials). Fail loudly with the reason (B1/B2) — a 502 with a readable
                    // {error} the UI surfaces, never a bare 500.
                    return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
                }
            }).RequireAuthorization();

        app.MapGet("/api/datasets/{id:guid}/eval-runs",
            async (Guid id, IEvalRunRepository repository, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var runs = await repository.ListByDatasetAsync(id, ct);
                return Results.Ok(runs.Select(EvalRunSummaryResponse.From));
            }).RequireAuthorization();

        app.MapGet("/api/eval-runs/{id:guid}",
            async (Guid id, IEvalRunRepository repository, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessEvalRunAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                var run = await repository.GetByIdAsync(id, ct);
                return run is null ? Results.NotFound() : Results.Ok(EvalRunResponse.From(run));
            }).RequireAuthorization();

        return app;
    }
}
