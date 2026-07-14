using Api.Auth;
using Application.Analytics;

namespace Api.Analytics;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics").RequireAuthorization();

        // Trend series across versions: Prompt × Dataset × Scorer.
        group.MapGet("/trends",
            async (Guid promptId, Guid datasetId, TrendAnalyticsHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if (await GateAsync(access, promptId, datasetId, ct) is { } problem)
                    return problem;
                var series = await handler.HandleAsync(promptId, datasetId, ct);
                return series is null
                    ? Results.NotFound()
                    : Results.Ok(series.Select(TrendSeriesResponse.From));
            });

        // Regression flags vs. the prior version; threshold/alpha are optional overrides.
        group.MapGet("/regressions",
            async (Guid promptId, Guid datasetId, double? threshold, double? alpha,
                   RegressionAnalyticsHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if (await GateAsync(access, promptId, datasetId, ct) is { } problem)
                    return problem;
                var flags = await handler.HandleAsync(promptId, datasetId, threshold, alpha, ct);
                return flags is null
                    ? Results.NotFound()
                    : Results.Ok(flags.Select(RegressionFlagResponse.From));
            });

        // Version-vs-version comparison: per-fixture + aggregate deltas per scorer.
        group.MapGet("/comparison",
            async (Guid promptId, Guid datasetId, Guid fromVersionId, Guid toVersionId,
                   ComparisonAnalyticsHandler handler, OrgAccess access, CancellationToken ct) =>
            {
                if (await GateAsync(access, promptId, datasetId, ct) is { } problem)
                    return problem;
                var comparison = await handler.HandleAsync(promptId, datasetId, fromVersionId, toVersionId, ct);
                return comparison is null
                    ? Results.NotFound()
                    : Results.Ok(VersionComparisonResponse.From(comparison));
            });

        return app;
    }

    // Analytics is keyed off a prompt (+its dataset), so access is gated on the prompt and, when
    // it resolves, its dataset (4.1). An unknown prompt stays a 404 as before; a foreign one is 403.
    private static async Task<IResult?> GateAsync(OrgAccess access, Guid promptId, Guid datasetId, CancellationToken ct)
    {
        if ((await access.CanAccessPromptAsync(promptId, ct)).ToProblem() is { } promptProblem)
            return promptProblem;
        return (await access.CanAccessDatasetAsync(datasetId, ct)).ToProblem();
    }
}
