using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class DatasetsEndpointTests : IAsyncLifetime
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

    private sealed record FixtureDto(
        Guid Id, string Origin, string Input, string? UpstreamContext, string? ExpectedOutput,
        Guid? SeedFixtureId, DateTimeOffset CreatedAt);
    private sealed record DatasetDto(Guid Id, string Name, string? Description, List<FixtureDto> Fixtures);
    private sealed record SummaryDto(
        Guid Id, string Name, string? Description, int FixtureCount, int CapturedCount, int SyntheticCount);

    [Fact]
    public async Task Create_then_capture_then_get_lands_fixtures_and_maps_the_capture_schema()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/datasets",
            new { name = "Summaries", description = "captured output" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();
        Assert.Empty(created!.Fixtures);

        var capture = await client.PostAsJsonAsync($"/api/datasets/{created.Id}/fixtures/capture", new
        {
            tuples = new[]
            {
                new { promptInput = "summarize this", input = "user asked", slmOutput = "raw slm output", downstreamResult = "the summary" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);

        var get = await client.GetAsync($"/api/datasets/{created.Id}");
        var fetched = await get.Content.ReadFromJsonAsync<DatasetDto>();
        var fixture = Assert.Single(fetched!.Fixtures);
        Assert.Equal("Captured", fixture.Origin);
        Assert.Equal("summarize this", fixture.Input);
        Assert.Equal("raw slm output", fixture.UpstreamContext);
        Assert.Equal("the summary", fixture.ExpectedOutput);
        Assert.Null(fixture.SeedFixtureId);
    }

    [Fact]
    public async Task Capture_redacts_pii_at_ingest()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/datasets", new { name = "Support", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();

        await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/capture", new
        {
            tuples = new[]
            {
                new { promptInput = "email bob@acme.com", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null },
            },
        });

        var fetched = await client.GetFromJsonAsync<DatasetDto>($"/api/datasets/{created.Id}");
        var fixture = Assert.Single(fetched!.Fixtures);
        Assert.DoesNotContain("bob@acme.com", fixture.Input);
        Assert.Contains("[REDACTED-EMAIL]", fixture.Input);
    }

    [Fact]
    public async Task List_returns_a_summary_with_origin_counts()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/datasets", new { name = "Counts", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();
        await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/capture", new
        {
            tuples = new[]
            {
                new { promptInput = "a", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null },
                new { promptInput = "b", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null },
            },
        });

        var list = await client.GetFromJsonAsync<List<SummaryDto>>("/api/datasets");

        var summary = Assert.Single(list!, s => s.Id == created.Id);
        Assert.Equal("Counts", summary.Name);
        Assert.Equal(2, summary.FixtureCount);
        Assert.Equal(2, summary.CapturedCount);
        Assert.Equal(0, summary.SyntheticCount);
    }

    [Fact]
    public async Task Get_unknown_dataset_returns_404()
    {
        var client = _factory.CreateClient();
        var get = await client.GetAsync($"/api/datasets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Capture_into_unknown_dataset_returns_404()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync($"/api/datasets/{Guid.NewGuid()}/fixtures/capture", new
        {
            tuples = new[] { new { promptInput = "x", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Create_with_blank_name_returns_400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/datasets", new { name = "   ", description = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
