using Application.Ports;

namespace Api.Models;

public static class ModelEndpoints
{
    public static IEndpointRouteBuilder MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/models").RequireAuthorization();

        // The catalog that feeds the target/judge droplists (1.13). Active entries only — any
        // authenticated user may read it; management is admin-gated (slice 4). Availability is
        // reflected from the eval-runner's configured providers (null when it's unreachable).
        group.MapGet("/", async (IModelCatalogRepository repository, IEvaluationRunner runner, CancellationToken ct) =>
        {
            var entries = await repository.ListAsync(includeInactive: false, ct);
            var configured = await runner.GetConfiguredProvidersAsync(ct);
            return Results.Ok(entries.Select(e => ModelResponse.From(e, configured)));
        });

        return app;
    }
}
