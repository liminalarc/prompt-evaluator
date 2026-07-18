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

public sealed class DatasetsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    // Stub generator: echoes back one synthetic fixture derived from the first seed, and
    // records the guidance/count it was called with. Keeps generation tests off the network.
    private sealed class StubRunner : IEvaluationRunner
    {
        public Task<string> EchoAsync(string prompt, CancellationToken ct = default) => Task.FromResult(prompt);
        public Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>?>(null);

        public Task<Application.ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => Task.FromResult<Application.ServiceVersion?>(null);

        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GeneratedFixtureData>>(
                new[] { new GeneratedFixtureData($"generated from: {seeds[0].Input}", "slm-shaped", null, 0) });

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
                services.AddScoped<IEvaluationRunner, StubRunner>();
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

    private sealed record FixtureDto(
        Guid Id, string Origin, string? Label, string? Description, string Input, string? UpstreamContext, string? ExpectedOutput,
        Guid? SeedFixtureId, DateTimeOffset CreatedAt);
    private sealed record DatasetDto(Guid Id, Guid PromptId, string Name, string? Description, List<FixtureDto> Fixtures);
    private sealed record SummaryDto(
        Guid Id, Guid PromptId, string Name, string? Description, int FixtureCount, int CapturedCount, int SyntheticCount);
    private sealed record PromptDto(Guid Id, string Name);

    // Datasets are created under a prompt (1.7), which belongs to an organization (1.9), so every
    // dataset test seeds an org + an owning prompt.
    private static async Task<Guid> CreatePromptAsync(HttpClient client, string name = "Owner")
    {
        var orgRes = await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" });
        var orgId = (await orgRes.Content.ReadFromJsonAsync<PromptDto>())!.Id;
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts", new { name, description = (string?)null });
        var prompt = await res.Content.ReadFromJsonAsync<PromptDto>();
        return prompt!.Id;
    }

    [Fact]
    public async Task Create_then_capture_then_get_lands_fixtures_and_maps_the_capture_schema()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets",
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
        var client = await _factory.CreateAuthenticatedClientAsync();

        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Support", description = (string?)null });
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
    public async Task Capture_can_mark_a_manual_fixture_synthetic_with_a_label()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Manual", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();

        await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/capture", new
        {
            tuples = new[]
            {
                new { promptInput = "hand written", input = (string?)null, slmOutput = (string?)null,
                    downstreamResult = (string?)null, origin = "Synthetic", label = "empty thread", description = "no rounds yet" },
            },
        });

        var fetched = await client.GetFromJsonAsync<DatasetDto>($"/api/datasets/{created.Id}");
        var fixture = Assert.Single(fetched!.Fixtures);
        Assert.Equal("Synthetic", fixture.Origin); // U8
        Assert.Null(fixture.SeedFixtureId);
        Assert.Equal("empty thread", fixture.Label);
        Assert.Equal("no rounds yet", fixture.Description);
    }

    [Fact]
    public async Task Capture_does_not_scrub_iso_dates_as_phone_numbers()
    {
        // B7: a date in captured text must survive ingest intact (was mangled to [REDACTED-PHONE]).
        var client = await _factory.CreateAuthenticatedClientAsync();
        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Dates", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();

        await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/capture", new
        {
            tuples = new[]
            {
                new { promptInput = "RECENT ROUNDS on 2026-07-12 were strong", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null },
            },
        });

        var fetched = await client.GetFromJsonAsync<DatasetDto>($"/api/datasets/{created.Id}");
        var fixture = Assert.Single(fetched!.Fixtures);
        Assert.Contains("2026-07-12", fixture.Input);
        Assert.DoesNotContain("REDACTED-PHONE", fixture.Input);
    }

    [Fact]
    public async Task Edit_fixture_updates_label_and_description()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Edit", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();
        await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/capture", new
        {
            tuples = new[] { new { promptInput = "input", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null } },
        });
        var withFixture = await client.GetFromJsonAsync<DatasetDto>($"/api/datasets/{created.Id}");
        var fixtureId = withFixture!.Fixtures[0].Id;

        var edit = await client.PatchAsJsonAsync($"/api/datasets/{created.Id}/fixtures/{fixtureId}",
            new { label = "renamed", description = "a scenario" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var fetched = await client.GetFromJsonAsync<DatasetDto>($"/api/datasets/{created.Id}");
        var fixture = Assert.Single(fetched!.Fixtures);
        Assert.Equal("renamed", fixture.Label);
        Assert.Equal("a scenario", fixture.Description);
        Assert.Equal("input", fixture.Input); // fixed
    }

    [Fact]
    public async Task Edit_unknown_fixture_returns_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "E404", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();

        var edit = await client.PatchAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/{Guid.NewGuid()}",
            new { label = "x", description = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
    }

    [Fact]
    public async Task List_returns_a_summary_with_origin_counts()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Counts", description = (string?)null });
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
    public async Task Generate_adds_synthetic_fixtures_linked_to_a_captured_seed()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Gen", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();
        await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/capture", new
        {
            tuples = new[] { new { promptInput = "seed input", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null } },
        });

        var generate = await client.PostAsJsonAsync($"/api/datasets/{created.Id}/fixtures/generate", new
        {
            guidance = new { coverageGoals = "cover more", edgeCases = (string?)null, constraints = (string?)null },
            count = 1,
        });
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        var fetched = await client.GetFromJsonAsync<DatasetDto>($"/api/datasets/{created.Id}");
        var captured = Assert.Single(fetched!.Fixtures, f => f.Origin == "Captured");
        var synthetic = Assert.Single(fetched.Fixtures, f => f.Origin == "Synthetic");
        Assert.Equal("generated from: seed input", synthetic.Input);
        Assert.Equal(captured.Id, synthetic.SeedFixtureId);
    }

    [Fact]
    public async Task Generate_with_no_captured_fixtures_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var promptId = await CreatePromptAsync(client);
        var create = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Bare", description = (string?)null });
        var created = await create.Content.ReadFromJsonAsync<DatasetDto>();

        var generate = await client.PostAsJsonAsync($"/api/datasets/{created!.Id}/fixtures/generate",
            new { guidance = (object?)null, count = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, generate.StatusCode);
    }

    [Fact]
    public async Task Generate_into_unknown_dataset_returns_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.PostAsJsonAsync($"/api/datasets/{Guid.NewGuid()}/fixtures/generate",
            new { guidance = (object?)null, count = 1 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_dataset_returns_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var get = await client.GetAsync($"/api/datasets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Capture_into_unknown_dataset_returns_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.PostAsJsonAsync($"/api/datasets/{Guid.NewGuid()}/fixtures/capture", new
        {
            tuples = new[] { new { promptInput = "x", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Create_with_blank_name_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var promptId = await CreatePromptAsync(client);
        var res = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "   ", description = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
