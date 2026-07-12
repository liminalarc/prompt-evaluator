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

        group.MapPost("/", async (CreatePromptRequest request, CreatePromptHandler handler, CancellationToken ct) =>
        {
            try
            {
                var prompt = await handler.HandleAsync(request.Name, request.Description, ct);
                return Results.Created($"/api/prompts/{prompt.Id}", PromptResponse.From(prompt));
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
