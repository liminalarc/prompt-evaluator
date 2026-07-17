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

public sealed class EvalHarnessEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    // Deterministic stub eval-runner: execution echoes "OUT:{input}"; the judge always passes.
    private sealed class StubRunner : IEvaluationRunner
    {
        public Task<string> EchoAsync(string prompt, CancellationToken ct = default) => Task.FromResult(prompt);
        public Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>?>(null);

        public Task<Application.ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => Task.FromResult<Application.ServiceVersion?>(null);
        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GeneratedFixtureData>>([]);
        public Task<PromptExecution> ExecutePromptAsync(
            string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
            => Task.FromResult(new PromptExecution($"OUT:{input}", 100, 1000, 500, 0.001m));
        public Task<JudgeVerdict> JudgeAsync(
            string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
            => Task.FromResult(new JudgeVerdict(0.9, true, $"judged by {judgeModel}"));
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

    private sealed record IdName(Guid Id);
    private sealed record PromptDto(Guid Id, List<VersionDto> Versions);
    private sealed record VersionDto(Guid Id);
    private sealed record ScorerDto(Guid Id, string Kind, string Identity, string? JudgeModel);
    private sealed record ScoreDto(string ScorerKind, string ScorerIdentity, string? JudgeModel, double Value, bool? Passed, string? Detail);
    private sealed record FixtureRunDto(Guid FixtureId, string ModelOutput, int LatencyMs, int InputTokens, int OutputTokens, decimal? CostUsd, List<ScoreDto> Scores);
    private sealed record RunDto(Guid Id, Guid PromptId, Guid PromptVersionId, Guid DatasetId, List<FixtureRunDto> Results);

    private async Task<(Guid promptId, Guid versionId, Guid datasetId)> SeedAsync(HttpClient client)
    {
        var orgRes = await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" });
        var orgId = (await orgRes.Content.ReadFromJsonAsync<IdName>())!.Id;
        var promptCreate = await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts", new { name = "Summarizer", description = (string?)null });
        var prompt = (await promptCreate.Content.ReadFromJsonAsync<PromptDto>())!;

        var versionRes = await client.PostAsJsonAsync($"/api/prompts/{prompt.Id}/versions",
            new { content = "You summarize.", targetModel = "claude-opus-4-8", label = (string?)null, sourceApp = (string?)null });
        var withVersion = (await versionRes.Content.ReadFromJsonAsync<PromptDto>())!;
        var versionId = withVersion.Versions[0].Id;

        var datasetCreate = await client.PostAsJsonAsync($"/api/prompts/{prompt.Id}/datasets", new { name = "Summaries", description = (string?)null });
        var dataset = (await datasetCreate.Content.ReadFromJsonAsync<IdName>())!;

        await client.PostAsJsonAsync($"/api/datasets/{dataset.Id}/fixtures/capture", new
        {
            tuples = new[] { new { promptInput = "hello world", input = (string?)null, slmOutput = "raw", downstreamResult = (string?)null } },
        });

        return (prompt.Id, versionId, dataset.Id);
    }

    [Fact]
    public async Task Configure_scorers_run_over_a_dataset_and_fetch_the_scored_result()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, versionId, datasetId) = await SeedAsync(client);

        // Configure a deterministic scorer and an LLM-judge scorer.
        var regexRes = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/scorers",
            new { kind = "Regex", config = "^OUT:", judgeModel = (string?)null });
        Assert.Equal(HttpStatusCode.Created, regexRes.StatusCode);
        var judgeRes = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/scorers",
            new { kind = "LlmJudge", config = "Is it a good summary?", judgeModel = "claude-opus-4-8" });
        Assert.Equal(HttpStatusCode.Created, judgeRes.StatusCode);

        var scorers = await client.GetFromJsonAsync<List<ScorerDto>>($"/api/datasets/{datasetId}/scorers");
        Assert.Equal(2, scorers!.Count);

        // Trigger a run.
        var runRes = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/eval-runs",
            new { promptId, promptVersionId = versionId });
        Assert.Equal(HttpStatusCode.Created, runRes.StatusCode);
        var run = (await runRes.Content.ReadFromJsonAsync<RunDto>())!;

        var fixtureRun = Assert.Single(run.Results);
        Assert.Equal("OUT:hello world", fixtureRun.ModelOutput);
        Assert.Equal(100, fixtureRun.LatencyMs);
        Assert.Equal(1000, fixtureRun.InputTokens);
        Assert.Equal(500, fixtureRun.OutputTokens);
        Assert.Equal(2, fixtureRun.Scores.Count); // deterministic + judge composed
        Assert.Contains(fixtureRun.Scores, s => s.ScorerKind == "Regex" && s.Value == 1.0);
        var judged = Assert.Single(fixtureRun.Scores, s => s.ScorerKind == "LlmJudge");
        Assert.Equal(0.9, judged.Value);
        Assert.Equal("claude-opus-4-8", judged.JudgeModel);

        // Fetch the persisted run back.
        var fetched = await client.GetFromJsonAsync<RunDto>($"/api/eval-runs/{run.Id}");
        Assert.Equal(run.Id, fetched!.Id);
        Assert.Equal(2, fetched.Results.Single().Scores.Count);
    }

    [Fact]
    public async Task Reruns_are_append_only_and_listed_for_the_dataset()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, versionId, datasetId) = await SeedAsync(client);
        await client.PostAsJsonAsync($"/api/datasets/{datasetId}/scorers",
            new { kind = "Regex", config = "^OUT:", judgeModel = (string?)null });

        var first = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/eval-runs", new { promptId, promptVersionId = versionId });
        var second = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/eval-runs", new { promptId, promptVersionId = versionId });
        var firstRun = (await first.Content.ReadFromJsonAsync<RunDto>())!;
        var secondRun = (await second.Content.ReadFromJsonAsync<RunDto>())!;

        Assert.NotEqual(firstRun.Id, secondRun.Id);
        var runs = await client.GetFromJsonAsync<List<RunDto>>($"/api/datasets/{datasetId}/eval-runs");
        Assert.Equal(2, runs!.Count);
    }

    [Fact]
    public async Task Configuring_a_scorer_on_an_unknown_dataset_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.PostAsJsonAsync($"/api/datasets/{Guid.NewGuid()}/scorers",
            new { kind = "Regex", config = "^x$", judgeModel = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_invalid_scorer_kind_is_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (_, _, datasetId) = await SeedAsync(client);
        var res = await client.PostAsJsonAsync($"/api/datasets/{datasetId}/scorers",
            new { kind = "nonsense", config = "x", judgeModel = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Running_against_an_unknown_dataset_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, versionId, _) = await SeedAsync(client);
        var res = await client.PostAsJsonAsync($"/api/datasets/{Guid.NewGuid()}/eval-runs",
            new { promptId, promptVersionId = versionId });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
