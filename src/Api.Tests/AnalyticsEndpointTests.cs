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

public sealed class AnalyticsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    // Stub eval-runner whose judge verdict depends on the prompt version's content, so different
    // versions produce different scores — the raw material analytics needs. Output echoes the
    // version content; a "good" version scores 0.9, otherwise 0.5.
    private sealed class VersionSensitiveRunner : IEvaluationRunner
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
            => Task.FromResult(new PromptExecution(promptContent, 100, 1000, 500, 0.001m));
        public Task<JudgeVerdict> JudgeAsync(
            string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
        {
            var score = output.Contains("good") ? 0.9 : 0.5;
            return Task.FromResult(new JudgeVerdict(score, score >= 0.7, "judged"));
        }
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEvaluationRunner>();
                services.AddScoped<IEvaluationRunner, VersionSensitiveRunner>();
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
    private sealed record VersionDto(Guid Id, int VersionNumber);
    private sealed record PromptDto(Guid Id, List<VersionDto> Versions);
    private sealed record ScorerRefDto(string Identity, string Kind, string? JudgeModel);
    private sealed record TrendPointDto(Guid PromptVersionId, int VersionNumber, string? VersionLabel, Guid RunId, double MeanValue, double? PassRate, int FixtureCount);
    private sealed record TrendSeriesDto(ScorerRefDto Scorer, List<TrendPointDto> Points);
    private sealed record RegressionFlagDto(ScorerRefDto Scorer, int FromVersionNumber, int ToVersionNumber, double PriorMean, double CurrentMean, double Delta, double? PValue, int PairedFixtureCount, string Confidence);
    private sealed record FixtureDeltaDto(Guid FixtureId, double? FromValue, double? ToValue, double? Delta);
    private sealed record ScorerComparisonDto(ScorerRefDto Scorer, double? FromMean, double? ToMean, double? Delta, List<FixtureDeltaDto> Fixtures);
    private sealed record ComparisonDto(int FromVersionNumber, int ToVersionNumber, List<ScorerComparisonDto> Scorers);
    private sealed record VarianceStatDto(double Mean, double StdDev, int SampleCount, double Min, double Max);
    private sealed record FixtureVarianceDto(Guid FixtureId, VarianceStatDto Value);
    private sealed record VersionVarianceDto(Guid PromptVersionId, int VersionNumber, int RunCount, VarianceStatDto Aggregate, List<FixtureVarianceDto> Fixtures);
    private sealed record ScorerVarianceDto(ScorerRefDto Scorer, List<VersionVarianceDto> Versions);

    // Seeds a prompt with two versions (v1 "good", v2 "bad"), a dataset with `fixtureCount`
    // fixtures, an LLM-judge scorer, and one run per version. Returns the ids.
    private async Task<(Guid promptId, Guid v1, Guid v2, Guid datasetId)> SeedTwoVersionsAsync(
        HttpClient client, int fixtureCount = 4)
    {
        var orgRes = await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" });
        var orgId = (await orgRes.Content.ReadFromJsonAsync<IdName>())!.Id;
        var promptRes = await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts", new { name = "Summarizer", description = (string?)null });
        var promptId = (await promptRes.Content.ReadFromJsonAsync<PromptDto>())!.Id;

        await client.PostAsJsonAsync($"/api/prompts/{promptId}/versions",
            new { content = "good summarizer", targetModel = "claude-opus-4-8", label = "baseline", sourceApp = (string?)null });
        var afterV2 = (await (await client.PostAsJsonAsync($"/api/prompts/{promptId}/versions",
            new { content = "bad summarizer", targetModel = "claude-opus-4-8", label = "tweaked", sourceApp = (string?)null }))
            .Content.ReadFromJsonAsync<PromptDto>())!;
        var v1 = afterV2.Versions.Single(v => v.VersionNumber == 1).Id;
        var v2 = afterV2.Versions.Single(v => v.VersionNumber == 2).Id;

        var datasetRes = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name = "Summaries", description = (string?)null });
        var datasetId = (await datasetRes.Content.ReadFromJsonAsync<IdName>())!.Id;
        var tuples = Enumerable.Range(0, fixtureCount)
            .Select(i => new { promptInput = $"fixture {i}", input = (string?)null, slmOutput = (string?)null, downstreamResult = (string?)null });
        await client.PostAsJsonAsync($"/api/datasets/{datasetId}/fixtures/capture", new { tuples });

        await client.PostAsJsonAsync($"/api/datasets/{datasetId}/scorers",
            new { kind = "LlmJudge", config = "Is it good?", judgeModel = "claude-opus-4-8" });

        await client.PostAsJsonAsync($"/api/datasets/{datasetId}/eval-runs", new { promptId, promptVersionId = v1 });
        await client.PostAsJsonAsync($"/api/datasets/{datasetId}/eval-runs", new { promptId, promptVersionId = v2 });

        return (promptId, v1, v2, datasetId);
    }

    [Fact]
    public async Task Trends_returns_one_point_per_version_ordered_by_version_number()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, _, _, datasetId) = await SeedTwoVersionsAsync(client);

        var series = await client.GetFromJsonAsync<List<TrendSeriesDto>>(
            $"/api/analytics/trends?promptId={promptId}&datasetId={datasetId}");

        var s = Assert.Single(series!);
        Assert.Equal("LlmJudge", s.Scorer.Kind);
        Assert.Equal([1, 2], s.Points.Select(p => p.VersionNumber).ToArray());
        Assert.Equal(0.9, s.Points[0].MeanValue, 3);
        Assert.Equal(0.5, s.Points[1].MeanValue, 3);
        Assert.Equal(4, s.Points[0].FixtureCount);
    }

    [Fact]
    public async Task Regressions_flags_the_significant_drop_and_respects_the_threshold_override()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, _, _, datasetId) = await SeedTwoVersionsAsync(client);

        var flags = await client.GetFromJsonAsync<List<RegressionFlagDto>>(
            $"/api/analytics/regressions?promptId={promptId}&datasetId={datasetId}");
        var flag = Assert.Single(flags!);
        Assert.Equal(1, flag.FromVersionNumber);
        Assert.Equal(2, flag.ToVersionNumber);
        Assert.Equal(-0.4, flag.Delta, 3);
        Assert.NotNull(flag.PValue);
        Assert.True(flag.PValue < 0.05);
        Assert.Equal("Confirmed", flag.Confidence);

        // A threshold larger than the 0.4 drop suppresses the flag.
        var suppressed = await client.GetFromJsonAsync<List<RegressionFlagDto>>(
            $"/api/analytics/regressions?promptId={promptId}&datasetId={datasetId}&threshold=0.5");
        Assert.Empty(suppressed!);
    }

    [Fact]
    public async Task Regressions_surfaces_a_single_fixture_drop_as_unverified()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        // A one-fixture dataset: the drop clears the threshold but n=1 → significance can't be
        // established, so the flag is surfaced as Unverified (a possible regression) not discarded.
        var (promptId, _, _, datasetId) = await SeedTwoVersionsAsync(client, fixtureCount: 1);

        var flags = await client.GetFromJsonAsync<List<RegressionFlagDto>>(
            $"/api/analytics/regressions?promptId={promptId}&datasetId={datasetId}");

        var flag = Assert.Single(flags!);
        Assert.Equal("Unverified", flag.Confidence);
        Assert.Equal(1, flag.PairedFixtureCount);
        Assert.Null(flag.PValue);
        Assert.Equal(-0.4, flag.Delta, 3);
    }

    [Fact]
    public async Task Comparison_returns_per_fixture_and_aggregate_deltas()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, v1, v2, datasetId) = await SeedTwoVersionsAsync(client);

        var cmp = await client.GetFromJsonAsync<ComparisonDto>(
            $"/api/analytics/comparison?promptId={promptId}&datasetId={datasetId}&fromVersionId={v1}&toVersionId={v2}");

        Assert.Equal(1, cmp!.FromVersionNumber);
        Assert.Equal(2, cmp.ToVersionNumber);
        var sc = Assert.Single(cmp.Scorers);
        Assert.Equal(0.9, sc.FromMean!.Value, 3);
        Assert.Equal(0.5, sc.ToMean!.Value, 3);
        Assert.Equal(-0.4, sc.Delta!.Value, 3);
        Assert.Equal(4, sc.Fixtures.Count);
        Assert.All(sc.Fixtures, f => Assert.Equal(-0.4, f.Delta!.Value, 3));
    }

    [Fact]
    public async Task Variance_aggregates_all_runs_of_a_version_not_just_the_latest()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, v1, _, datasetId) = await SeedTwoVersionsAsync(client);
        // Re-run v1: variance must aggregate BOTH runs (trends would keep only the latest).
        await client.PostAsJsonAsync($"/api/datasets/{datasetId}/eval-runs", new { promptId, promptVersionId = v1 });

        var series = await client.GetFromJsonAsync<List<ScorerVarianceDto>>(
            $"/api/analytics/variance?promptId={promptId}&datasetId={datasetId}");

        var s = Assert.Single(series!);
        var v1Var = s.Versions.Single(v => v.VersionNumber == 1);
        Assert.Equal(2, v1Var.RunCount); // both runs aggregated
        // The stub judge is deterministic, so the mean holds and the spread is zero — the endpoint's
        // job here is proving it aggregates repeats; non-zero-spread math is covered by unit tests.
        Assert.Equal(0.9, v1Var.Aggregate.Mean, 3);
        Assert.Equal(0.0, v1Var.Aggregate.StdDev, 3);
        Assert.Equal(4, v1Var.Fixtures.Count);
    }

    [Fact]
    public async Task Variance_for_an_unknown_prompt_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.GetAsync($"/api/analytics/variance?promptId={Guid.NewGuid()}&datasetId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Trends_for_an_unknown_prompt_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.GetAsync($"/api/analytics/trends?promptId={Guid.NewGuid()}&datasetId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Comparison_with_an_unknown_version_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var (promptId, v1, _, datasetId) = await SeedTwoVersionsAsync(client);
        var res = await client.GetAsync(
            $"/api/analytics/comparison?promptId={promptId}&datasetId={datasetId}&fromVersionId={v1}&toVersionId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
