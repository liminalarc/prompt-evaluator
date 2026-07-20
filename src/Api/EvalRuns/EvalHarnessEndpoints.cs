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
                if (!TryBuildDescriptor(request, out var descriptor, out var error)
                    || !TryResolveWeight(request, out var weight, out error))
                    return Results.BadRequest(new { error });

                var config = await handler.HandleAsync(id, descriptor!, weight, ct);
                return config is null
                    ? Results.NotFound()
                    : Results.Created($"/api/datasets/{id}/scorers", ScorerConfigResponse.From(config));
            }).RequireAuthorization();

        // Edit a configured scorer in place (U9) — replaces its descriptor.
        app.MapPut("/api/datasets/{id:guid}/scorers/{scorerId:guid}",
            async (Guid id, Guid scorerId, ConfigureScorerRequest request, ConfigureDatasetScorersHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                if (!TryBuildDescriptor(request, out var descriptor, out var error)
                    || !TryResolveWeight(request, out var weight, out error))
                    return Results.BadRequest(new { error });

                var config = await handler.ReconfigureAsync(id, scorerId, descriptor!, weight, ct);
                return config is null ? Results.NotFound() : Results.Ok(ScorerConfigResponse.From(config));
            }).RequireAuthorization();

        // Remove a scorer from a dataset's set (U9).
        app.MapDelete("/api/datasets/{id:guid}/scorers/{scorerId:guid}",
            async (Guid id, Guid scorerId, ConfigureDatasetScorersHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if ((await access.CanAccessDatasetAsync(id, ct)).ToProblem() is { } problem)
                    return problem;
                return await handler.RemoveAsync(id, scorerId, ct) ? Results.NoContent() : Results.NotFound();
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

    // Builds a scorer descriptor from the request, or reports why it is invalid (unknown kind, or a
    // required config/rubric/judge model missing). Shared by create (POST) and edit (PUT).
    private static bool TryBuildDescriptor(ConfigureScorerRequest request, out ScorerDescriptor? descriptor, out string? error)
    {
        descriptor = null;
        error = null;
        if (!Enum.TryParse<ScorerKind>(request.Kind, ignoreCase: true, out var kind))
        {
            error = $"Unknown scorer kind '{request.Kind}'.";
            return false;
        }
        try
        {
            descriptor = kind == ScorerKind.LlmJudge
                ? ScorerDescriptor.LlmJudge(request.Config ?? "", request.JudgeModel ?? "")
                : ScorerDescriptor.Deterministic(kind, request.Config);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // Resolves the composite weight from the request (2.9): omitted → 1.0 (equal weighting); a
    // non-finite or non-positive value is rejected with a readable 400. Shared by create + edit.
    private static bool TryResolveWeight(ConfigureScorerRequest request, out double weight, out string? error)
    {
        error = null;
        weight = request.Weight ?? 1.0;
        if (!double.IsFinite(weight) || weight <= 0.0)
        {
            error = "Scorer weight must be a finite, strictly positive number.";
            weight = 1.0;
            return false;
        }
        return true;
    }
}
