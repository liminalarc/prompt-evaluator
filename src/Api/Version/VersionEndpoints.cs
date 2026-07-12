using Application;
using Application.Ports;

namespace Api.Version;

public static class VersionEndpoints
{
    private const string ServiceName = "litmus-ai-api";
    private const string ServiceVersionNumber = "0.2.0";

    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/version", async (
            IConfiguration config,
            IEvaluationRunner runner,
            ISystemInfo systemInfo,
            CancellationToken ct) =>
        {
            var commit = config["GIT_COMMIT"] ?? "dev";

            // Dependencies are probed live; a downed dependency yields null rather than a 500.
            ServiceVersion? runnerVersion = null;
            try { runnerVersion = await runner.GetVersionAsync(ct); }
            catch { /* eval-runner unreachable */ }

            string? dbVersion = null;
            try { dbVersion = await systemInfo.GetDatabaseVersionAsync(ct); }
            catch { /* db unreachable */ }

            var response = new VersionResponse(
                ServiceName,
                ServiceVersionNumber,
                commit,
                new DependencyVersions(
                    runnerVersion is null
                        ? null
                        : new ServiceVersionDto(runnerVersion.Service, runnerVersion.Version, runnerVersion.Commit),
                    dbVersion is null ? null : new DbVersionDto(dbVersion)));

            return Results.Ok(response);
        });

        return app;
    }

    private sealed record VersionResponse(
        string Service, string Version, string Commit, DependencyVersions Dependencies);

    private sealed record DependencyVersions(ServiceVersionDto? EvalRunner, DbVersionDto? Db);

    private sealed record ServiceVersionDto(string Service, string Version, string Commit);

    private sealed record DbVersionDto(string Version);
}
