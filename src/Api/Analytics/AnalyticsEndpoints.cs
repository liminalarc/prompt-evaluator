using Application.Analytics;

namespace Api.Analytics;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        // Trend series across versions: Prompt × Dataset × Scorer.
        app.MapGet("/api/analytics/trends",
            async (Guid promptId, Guid datasetId, TrendAnalyticsHandler handler, CancellationToken ct) =>
            {
                var series = await handler.HandleAsync(promptId, datasetId, ct);
                return series is null
                    ? Results.NotFound()
                    : Results.Ok(series.Select(TrendSeriesResponse.From));
            });

        // Regression flags vs. the prior version; threshold/alpha are optional overrides.
        app.MapGet("/api/analytics/regressions",
            async (Guid promptId, Guid datasetId, double? threshold, double? alpha,
                   RegressionAnalyticsHandler handler, CancellationToken ct) =>
            {
                var flags = await handler.HandleAsync(promptId, datasetId, threshold, alpha, ct);
                return flags is null
                    ? Results.NotFound()
                    : Results.Ok(flags.Select(RegressionFlagResponse.From));
            });

        // Version-vs-version comparison: per-fixture + aggregate deltas per scorer.
        app.MapGet("/api/analytics/comparison",
            async (Guid promptId, Guid datasetId, Guid fromVersionId, Guid toVersionId,
                   ComparisonAnalyticsHandler handler, CancellationToken ct) =>
            {
                var comparison = await handler.HandleAsync(promptId, datasetId, fromVersionId, toVersionId, ct);
                return comparison is null
                    ? Results.NotFound()
                    : Results.Ok(VersionComparisonResponse.From(comparison));
            });

        return app;
    }
}
