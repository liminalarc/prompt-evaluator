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

public sealed class EvalRunsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class FakeEchoRunner : IEvaluationRunner
    {
        public Task<string> EchoAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(prompt);
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

    private sealed record EvalRunDto(Guid Id, string Prompt, string Output, DateTimeOffset CreatedAt);

    [Fact]
    public async Task Post_creates_run_then_Get_retrieves_the_persisted_row()
    {
        var client = _factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/eval-runs", new { prompt = "round trip" });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<EvalRunDto>();
        Assert.NotNull(created);
        Assert.Equal("round trip", created!.Prompt);
        Assert.Equal("round trip", created.Output);
        Assert.NotEqual(Guid.Empty, created.Id);

        var get = await client.GetAsync($"/api/eval-runs/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<EvalRunDto>();
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("round trip", fetched.Output);
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var client = _factory.CreateClient();
        var get = await client.GetAsync($"/api/eval-runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
