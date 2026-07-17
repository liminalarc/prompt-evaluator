using Api.Auth;
using Application.Models;
using Application.Ports;

namespace Api.Models;

public static class ModelEndpoints
{
    public static IEndpointRouteBuilder MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/models").RequireAuthorization();

        // The catalog that feeds the target/judge droplists (1.13). Active entries only for the
        // droplists; a global admin may pass ?includeInactive=true for the management page.
        // Availability is reflected from the eval-runner's configured providers (null when unreachable).
        group.MapGet("/", async (
            bool? includeInactive,
            IModelCatalogRepository repository,
            IEvaluationRunner runner,
            OrgAccess access,
            CancellationToken ct) =>
        {
            var wantInactive = includeInactive == true;
            if (wantInactive && !await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var entries = await repository.ListAsync(wantInactive, ct);
            var configured = await runner.GetConfiguredProvidersAsync(ct);
            return Results.Ok(entries.Select(e => ModelResponse.From(e, configured)));
        });

        // Admin management (1.13) — gated by the workspace-level global-admin flag. Non-admins 403.
        group.MapPost("/", async (
            CreateModelRequest request, CreateModelHandler handler, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            try
            {
                var entry = await handler.HandleAsync(
                    request.ModelId, request.DisplayName, request.Provider, request.Roles,
                    request.InputPricePerMTokUsd, request.OutputPricePerMTokUsd, ct);
                return Results.Created($"/api/models/{entry.Id}", ModelResponse.From(entry, null));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateModelRequest request, UpdateModelHandler handler, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            try
            {
                var entry = await handler.HandleAsync(
                    id, request.DisplayName, request.Provider, request.Roles,
                    request.InputPricePerMTokUsd, request.OutputPricePerMTokUsd, ct);
                return entry is null ? Results.NotFound() : Results.Ok(ModelResponse.From(entry, null));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/deactivate", async (
            Guid id, SetModelActiveHandler handler, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var entry = await handler.HandleAsync(id, isActive: false, ct);
            return entry is null ? Results.NotFound() : Results.Ok(ModelResponse.From(entry, null));
        });

        group.MapPost("/{id:guid}/activate", async (
            Guid id, SetModelActiveHandler handler, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var entry = await handler.HandleAsync(id, isActive: true, ct);
            return entry is null ? Results.NotFound() : Results.Ok(ModelResponse.From(entry, null));
        });

        return app;
    }
}
