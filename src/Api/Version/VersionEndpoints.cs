using System.Reflection;
using Application;
using Application.Ports;

namespace Api.Version;

public static class VersionEndpoints
{
    private const string ServiceName = "litmus-ai-api";

    // The version is the git tag, stamped into the assembly at build (-p:Version=$APP_VERSION);
    // "0.0.0-dev" for local/dev builds. The SDK may append "+<sha>" (SourceRevisionId) — trim it,
    // since the commit is reported separately below.
    private static readonly string ServiceVersionNumber =
        (typeof(VersionEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0-dev")
        .Split('+')[0];

    // Best-effort build timestamp: the assembly file's last-write time in UTC (stable within an
    // image). "unknown" if it can't be read (e.g. a single-file publish with no location on disk).
    private static readonly string BuildTime = ResolveBuildTime();

    private static string ResolveBuildTime()
    {
        try
        {
            var location = typeof(VersionEndpoints).Assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
                return File.GetLastWriteTimeUtc(location).ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
        catch { /* fall through */ }
        return "unknown";
    }

    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        // Flat, single-service payload the SPA consumes for its footer chip + env badge (3.3,
        // Stormboard pattern). Distinct from the aggregated GET /version below (which fans out to
        // eval-runner + db). `channel` — a DEPLOY_CHANNEL env baked at build from a CI build-arg by
        // git-ref (tag -> prod, main -> dev, local -> "local") — is the reliable dev/prod signal;
        // `environment` (ASPNETCORE_ENVIRONMENT) is reported but deliberately NOT the discriminator.
        app.MapGet("/api/version", (IConfiguration config, IHostEnvironment env) =>
        {
            var commit = config["GIT_COMMIT"] ?? "dev";
            var channel = config["DEPLOY_CHANNEL"] ?? "local";
            return Results.Ok(new VersionInfo(
                ServiceVersionNumber, commit, BuildTime, env.EnvironmentName, channel));
        });

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

    private sealed record VersionInfo(
        string Version, string Commit, string BuildTime, string Environment, string Channel);

    private sealed record VersionResponse(
        string Service, string Version, string Commit, DependencyVersions Dependencies);

    private sealed record DependencyVersions(ServiceVersionDto? EvalRunner, DbVersionDto? Db);

    private sealed record ServiceVersionDto(string Service, string Version, string Commit);

    private sealed record DbVersionDto(string Version);
}
