using System.Net;
using System.Net.Http.Json;
using Application;
using Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class ModelsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:Postgres", connectionString);
    }

    // Boots the app with a stubbed eval-runner so availability can be asserted deterministically
    // (the real HTTP client has no eval-runner to reach in tests).
    private sealed class FactoryWithRunner(string connectionString, IEvaluationRunner runner)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEvaluationRunner>();
                services.AddScoped(_ => runner);
            });
        }
    }

    // Only GetConfiguredProvidersAsync is exercised here; the rest are not called.
    private sealed class FakeRunner(IReadOnlyList<string>? providers) : IEvaluationRunner
    {
        public Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
            => Task.FromResult(providers);

        public Task<string> EchoAsync(string prompt, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<PromptExecution> ExecutePromptAsync(
            string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<JudgeVerdict> JudgeAsync(
            string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
            => throw new NotSupportedException();
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

    private sealed record ModelDto(
        Guid Id, string ModelId, string DisplayName, string Provider,
        List<string> Roles, decimal? InputPricePerMTokUsd, decimal? OutputPricePerMTokUsd,
        bool IsActive, bool Available);

    [Fact]
    public async Task Get_returns_the_seeded_catalog_with_id_name_provider_and_roles()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        Assert.NotNull(models);
        var opus = Assert.Single(models!, m => m.ModelId == "claude-opus-4-8");
        Assert.Equal("Claude Opus 4.8", opus.DisplayName);
        Assert.Equal("Anthropic", opus.Provider);
        Assert.Equal(new[] { "subject", "judge", "generator" }, opus.Roles);
        Assert.Equal(5m, opus.InputPricePerMTokUsd);
        Assert.Equal(25m, opus.OutputPricePerMTokUsd);
        Assert.True(opus.IsActive);

        // All five seeded models are present, across both providers.
        var ids = models!.Select(m => m.ModelId).ToList();
        Assert.Contains("claude-sonnet-5", ids);
        Assert.Contains("claude-haiku-4-5", ids);
        Assert.Contains("gpt-4o", ids);
        Assert.Contains("gpt-4o-mini", ids);
    }

    [Fact]
    public async Task Get_offers_a_non_claude_model_with_null_pricing()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        var mini = Assert.Single(models!, m => m.ModelId == "gpt-4o-mini");
        Assert.Equal("OpenAi", mini.Provider);
        Assert.Null(mini.InputPricePerMTokUsd);
        Assert.Null(mini.OutputPricePerMTokUsd);
    }

    [Fact]
    public async Task Get_requires_authentication()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/models");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Get_marks_a_model_unavailable_when_its_provider_has_no_configured_credentials()
    {
        // The eval-runner reports only Anthropic configured — OpenAI models are unavailable.
        using var factory = new FactoryWithRunner(_postgres.GetConnectionString(), new FakeRunner(new[] { "anthropic" }));
        var client = await factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        Assert.True(Assert.Single(models!, m => m.ModelId == "claude-opus-4-8").Available);
        Assert.False(Assert.Single(models!, m => m.ModelId == "gpt-4o").Available);
    }

    [Fact]
    public async Task Get_treats_every_model_available_when_the_eval_runner_is_unreachable()
    {
        // Null configured-providers = unknown; models must not be hidden.
        using var factory = new FactoryWithRunner(_postgres.GetConnectionString(), new FakeRunner(null));
        var client = await factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        Assert.All(models!, m => Assert.True(m.Available));
    }
}
