using Application.Ports;

namespace Api.Models;

public static class ModelEndpoints
{
    public static IEndpointRouteBuilder MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/models").RequireAuthorization();

        // The catalog that feeds the target/judge droplists (1.13). Active entries only — any
        // authenticated user may read it; management is admin-gated (slice 4).
        group.MapGet("/", async (IModelCatalogRepository repository, CancellationToken ct) =>
        {
            var entries = await repository.ListAsync(includeInactive: false, ct);
            return Results.Ok(entries.Select(ModelResponse.From));
        });

        return app;
    }
}
