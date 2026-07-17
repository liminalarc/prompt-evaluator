using System.Net;
using System.Net.Http.Json;
using Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class SeamEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class FakeEchoRunner : IEvaluationRunner
    {
        public Task<string> EchoAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(prompt);

        public Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>?>(null);

        public Task<Application.ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => Task.FromResult<Application.ServiceVersion?>(
                new Application.ServiceVersion("eval-runner", "0.1.0", "faketest"));

        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GeneratedFixtureData>>([]);

        public Task<PromptExecution> ExecutePromptAsync(string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<JudgeVerdict> JudgeAsync(string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEvaluationRunner>();
                services.AddScoped<IEvaluationRunner, FakeEchoRunner>();
            });
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new Factory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed record EchoDto(string Output);

    [Fact]
    public async Task Echo_round_trips_the_prompt_through_the_eval_runner()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/echo", new { prompt = "round trip" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<EchoDto>();
        Assert.Equal("round trip", body!.Output);
    }

    [Fact]
    public async Task Echo_rejects_a_blank_prompt()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/echo", new { prompt = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private sealed record VersionDto(string Service, string Version, string Commit, DepsDto Dependencies);
    private sealed record DepsDto(SvcDto? EvalRunner, DbDto? Db);
    private sealed record SvcDto(string Service, string Version, string Commit);
    private sealed record DbDto(string Version);

    [Fact]
    public async Task Version_aggregates_api_eval_runner_and_db()
    {
        var client = _factory.CreateClient();

        var res = await client.GetFromJsonAsync<VersionDto>("/version");

        Assert.NotNull(res);
        Assert.Equal("litmus-ai-api", res!.Service);
        // Version is git-tag-derived and stamped at build; tests assert its shape, never a literal,
        // so a release bump never breaks this. ("0.0.0-dev" for the test build.)
        Assert.Matches(@"^\d+\.\d+\.\d+", res.Version);
        Assert.Equal("eval-runner", res.Dependencies.EvalRunner!.Service);
        Assert.Equal("faketest", res.Dependencies.EvalRunner.Commit);
        Assert.StartsWith("PostgreSQL", res.Dependencies.Db!.Version);
    }
}
