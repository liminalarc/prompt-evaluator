using Application.Folders;
using Application.Ports;
using Application.Prompts;

namespace Api.Prompts;

public static class PromptEndpoints
{
    public static IEndpointRouteBuilder MapPromptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prompts");

        group.MapGet("/", async (IPromptRepository repository, CancellationToken ct) =>
        {
            var prompts = await repository.ListAsync(ct);
            return Results.Ok(prompts.Select(PromptSummaryResponse.From));
        });

        group.MapGet("/{id:guid}", async (Guid id, IPromptRepository repository, CancellationToken ct) =>
        {
            var prompt = await repository.GetByIdAsync(id, ct);
            return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
        });

        // Prompts belong to an organization (1.9): created and browsed under their org.
        app.MapGet("/api/organizations/{orgId:guid}/prompts",
            async (Guid orgId, IPromptRepository repository, CancellationToken ct) =>
            {
                var prompts = await repository.ListByOrganizationAsync(orgId, ct);
                return Results.Ok(prompts.Select(PromptSummaryResponse.From));
            });

        app.MapPost("/api/organizations/{orgId:guid}/prompts",
            async (Guid orgId, CreatePromptRequest request, CreatePromptHandler handler, CancellationToken ct) =>
            {
                try
                {
                    var prompt = await handler.HandleAsync(orgId, request.Name, request.Description, ct);
                    return prompt is null
                        ? Results.NotFound(new { error = "Organization not found." })
                        : Results.Created($"/api/prompts/{prompt.Id}", PromptResponse.From(prompt));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        // Move a prompt into a folder (1.7), or unfile it to the root when FolderId is null.
        group.MapPost("/{id:guid}/move",
            async (Guid id, MovePromptRequest request, MovePromptHandler handler, CancellationToken ct) =>
            {
                try
                {
                    var prompt = await handler.HandleAsync(id, request.FolderId, ct);
                    return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        group.MapPost("/{id:guid}/versions",
            async (Guid id, AddPromptVersionRequest request, AddPromptVersionHandler handler, CancellationToken ct) =>
            {
                try
                {
                    var prompt = await handler.HandleAsync(
                        id, request.Content, request.TargetModel, request.Label, request.SourceApp, ct);
                    return prompt is null ? Results.NotFound() : Results.Ok(PromptResponse.From(prompt));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

        return app;
    }
}
