using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class PromptsEndpointTests : IAsyncLifetime
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

    private sealed record VersionDto(
        Guid Id, int VersionNumber, string Content, string TargetModel, string? Label, string? SourceApp, DateTimeOffset CreatedAt);
    private sealed record PromptDto(Guid Id, string Name, string? Description, List<VersionDto> Versions);
    private sealed record SummaryDto(Guid Id, string Name, string? Description, int VersionCount, string? LatestTargetModel);

    [Fact]
    public async Task Create_add_version_then_get_round_trips_full_history_with_target_model()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/prompts",
            new { name = "Summarizer", description = "Summarizes captured output" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PromptDto>();
        Assert.NotNull(created);
        Assert.Equal("Summarizer", created!.Name);
        Assert.Empty(created.Versions);

        var addVersion = await client.PostAsJsonAsync($"/api/prompts/{created.Id}/versions",
            new { content = "Summarize: {input}", targetModel = "claude-sonnet-5", label = "baseline", sourceApp = "Stormboard" });
        Assert.Equal(HttpStatusCode.OK, addVersion.StatusCode);

        var get = await client.GetAsync($"/api/prompts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<PromptDto>();
        var version = Assert.Single(fetched!.Versions);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("Summarize: {input}", version.Content);
        Assert.Equal("claude-sonnet-5", version.TargetModel);
        Assert.Equal("baseline", version.Label);
        Assert.Equal("Stormboard", version.SourceApp);
    }

    [Fact]
    public async Task List_returns_a_summary_including_the_created_prompt()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/prompts", new { name = "Classifier", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<PromptDto>();
        await client.PostAsJsonAsync($"/api/prompts/{created!.Id}/versions",
            new { content = "Classify: {input}", targetModel = "claude-opus-4-8", label = (string?)null, sourceApp = (string?)null });

        var list = await client.GetFromJsonAsync<List<SummaryDto>>("/api/prompts");

        var summary = Assert.Single(list!, s => s.Id == created.Id);
        Assert.Equal("Classifier", summary.Name);
        Assert.Equal(1, summary.VersionCount);
        Assert.Equal("claude-opus-4-8", summary.LatestTargetModel);
    }

    [Fact]
    public async Task Get_unknown_prompt_returns_404()
    {
        var client = _factory.CreateClient();
        var get = await client.GetAsync($"/api/prompts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Add_version_to_unknown_prompt_returns_404()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync($"/api/prompts/{Guid.NewGuid()}/versions",
            new { content = "x", targetModel = "claude-sonnet-5", label = (string?)null, sourceApp = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Create_with_blank_name_returns_400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/prompts", new { name = "   ", description = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
