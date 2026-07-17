using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        List<string> Roles, decimal? InputPricePerMTokUsd, decimal? OutputPricePerMTokUsd, bool IsActive);

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
}
