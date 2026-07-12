using Application.Ports;

namespace Api.Seam;

/// <summary>
/// The walking-skeleton seam check (from 0.1): a trivial round-trip through the eval-runner's
/// <c>/echo</c>. It proves the API ↔ eval-runner path end-to-end without persisting anything —
/// <see cref="Domain.EvalRun"/> is now the real evaluation aggregate, so the round-trip lives here.
/// </summary>
public static class EchoEndpoints
{
    public static IEndpointRouteBuilder MapEchoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/echo", async (EchoRequest request, IEvaluationRunner runner, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest(new { error = "Prompt must not be blank." });

            var output = await runner.EchoAsync(request.Prompt, ct);
            return Results.Ok(new EchoResponse(output));
        });

        return app;
    }
}

public sealed record EchoRequest(string Prompt);

public sealed record EchoResponse(string Output);
