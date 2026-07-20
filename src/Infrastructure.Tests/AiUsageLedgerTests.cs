using System.Net;
using System.Net.Http.Json;
using Application.AiUsage;
using Application.Ports;
using Domain;
using Infrastructure.Http;
using Infrastructure.Persistence;
using Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

/// <summary>
/// 6.1: every model call through <see cref="EvalRunnerClient"/> records an <see cref="AiUsageRecord"/>
/// on success and failure, attributed via the ambient context. Fast unit tests use a fake recorder;
/// the integration test at the bottom proves the records actually persist through the real EF recorder.
/// </summary>
public class AiUsageLedgerTests
{
    internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }

    private sealed class FakeRecorder : IAiUsageRecorder
    {
        public List<AiUsageRecord> Records { get; } = [];
        public Task RecordAsync(AiUsageRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    internal static HttpClient HttpWith(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new StubHandler(respond)) { BaseAddress = new Uri("http://eval-runner:8000") };

    internal static object UsageJson(string model = "claude-opus-4-8") => new
    {
        model,
        input_tokens = 1000,
        output_tokens = 500,
        cache_creation_input_tokens = 40,
        cache_read_input_tokens = 960,
        request_id = "req_abc",
        status = "success",
        max_tokens = 4096,
    };

    private static HttpResponseMessage Ok(object body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    [Fact]
    public async Task Execute_records_a_subject_execution_usage_record_with_attribution()
    {
        var recorder = new FakeRecorder();
        var ctx = new AmbientAiUsageContext();
        var http = HttpWith(_ => Ok(new
        {
            output = "o", latency_ms = 10, input_tokens = 1000, output_tokens = 500,
            cost_usd = 0.0175, usage = UsageJson(),
        }));
        var client = new EvalRunnerClient(http, recorder, ctx, TimeProvider.System);

        var org = Guid.NewGuid();
        var user = Guid.NewGuid();
        using (ctx.Begin(new AiUsageAttribution(org, user)))
            await client.ExecutePromptAsync("p", "claude-opus-4-8", "in", null);

        var rec = Assert.Single(recorder.Records);
        Assert.Equal(AiUsageFeature.SubjectExecution, rec.Feature);
        Assert.Equal(AiUsageStatus.Success, rec.Status);
        Assert.Equal("claude-opus-4-8", rec.Model);
        Assert.Equal(org, rec.OrganizationId);
        Assert.Equal(user, rec.UserId);
        Assert.Equal(1000, rec.InputTokens);
        Assert.Equal(500, rec.OutputTokens);
        Assert.Equal(40, rec.CacheCreationTokens);
        Assert.Equal(960, rec.CacheReadTokens);
        Assert.Equal("req_abc", rec.RequestId);
        Assert.Equal(4096, rec.MaxTokens);
        Assert.True(rec.LatencyMs >= 0);
    }

    [Fact]
    public async Task Judge_records_an_llm_judge_usage_record()
    {
        var recorder = new FakeRecorder();
        var http = HttpWith(_ => Ok(new
        {
            score = 0.8, passed = true, rationale = "ok", usage = UsageJson(),
        }));
        var client = new EvalRunnerClient(http, recorder, new AmbientAiUsageContext(), TimeProvider.System);

        await client.JudgeAsync("r", "i", "a", null, "claude-opus-4-8");

        var rec = Assert.Single(recorder.Records);
        Assert.Equal(AiUsageFeature.LlmJudge, rec.Feature);
        Assert.Equal(AiUsageStatus.Success, rec.Status);
        Assert.Equal("claude-opus-4-8", rec.Model);
        Assert.Equal(1000, rec.InputTokens);
    }

    [Fact]
    public async Task Generate_records_a_synthetic_generation_usage_record()
    {
        var recorder = new FakeRecorder();
        var http = HttpWith(_ => Ok(new { fixtures = Array.Empty<object>(), usage = UsageJson() }));
        var client = new EvalRunnerClient(http, recorder, new AmbientAiUsageContext(), TimeProvider.System);

        await client.GenerateSyntheticFixturesAsync(
            new[] { new SeedExampleData("captured", null, null) },
            new GenerationGuidanceData(null, null, null),
            count: 1);

        var rec = Assert.Single(recorder.Records);
        Assert.Equal(AiUsageFeature.SyntheticGeneration, rec.Feature);
        Assert.Equal(AiUsageStatus.Success, rec.Status);
        Assert.Equal("claude-opus-4-8", rec.Model); // echoed by the usage block
    }

    [Fact]
    public async Task A_failed_call_records_an_error_usage_record_with_the_requested_model()
    {
        var recorder = new FakeRecorder();
        var http = HttpWith(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { detail = "provider not configured" }),
        });
        var client = new EvalRunnerClient(http, recorder, new AmbientAiUsageContext(), TimeProvider.System);

        await Assert.ThrowsAsync<EvalRunnerException>(() =>
            client.JudgeAsync("r", "i", "a", null, "claude-opus-4-8"));

        var rec = Assert.Single(recorder.Records);
        Assert.Equal(AiUsageFeature.LlmJudge, rec.Feature);
        Assert.Equal(AiUsageStatus.Error, rec.Status);
        Assert.Equal("claude-opus-4-8", rec.Model); // no usage came back → the requested model
        Assert.Equal(0, rec.InputTokens);
    }

    [Fact]
    public async Task Refusal_status_from_the_usage_block_is_recorded()
    {
        var recorder = new FakeRecorder();
        var http = HttpWith(_ => Ok(new
        {
            output = "", latency_ms = 5, input_tokens = 10, output_tokens = 0, cost_usd = (decimal?)null,
            usage = new
            {
                model = "claude-fable-5", input_tokens = 10, output_tokens = 0,
                cache_creation_input_tokens = 0, cache_read_input_tokens = 0,
                request_id = "req_r", status = "refusal", max_tokens = 4096,
            },
        }));
        var client = new EvalRunnerClient(http, recorder, new AmbientAiUsageContext(), TimeProvider.System);

        await client.ExecutePromptAsync("p", "claude-fable-5", "in", null);

        var rec = Assert.Single(recorder.Records);
        Assert.Equal(AiUsageStatus.Refusal, rec.Status);
    }

    [Fact]
    public async Task No_recorder_configured_is_a_no_op_and_does_not_break_the_call()
    {
        // The bare ctor (used widely in tests) must still work — recording is simply skipped.
        var http = HttpWith(_ => Ok(new
        {
            score = 0.5, passed = true, rationale = "ok", usage = UsageJson(),
        }));
        var client = new EvalRunnerClient(http);

        var verdict = await client.JudgeAsync("r", "i", "a", null, "claude-opus-4-8");

        Assert.Equal(0.5, verdict.Score);
    }
}

/// <summary>
/// Integration test: a run (execute + judge) persists usage records through the real EF
/// <see cref="AiUsageRecorder"/> to Postgres (6.1 AC — "a run persists records").
/// </summary>
public sealed class AiUsageRecorderIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddDbContext<EvalDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EvalDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task A_run_persists_a_usage_record_per_model_call()
    {
        var pricing = new ConfigUsagePricing(Options.Create(AiUsagePricingOptions.Defaults()));
        var recorder = new AiUsageRecorder(_provider.GetRequiredService<IServiceScopeFactory>(), pricing);
        var ctx = new AmbientAiUsageContext();
        var org = Guid.NewGuid();
        var user = Guid.NewGuid();

        var http = AiUsageLedgerTests.HttpWith(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = path == "/execute-prompt"
                    ? JsonContent.Create(new
                    {
                        output = "o", latency_ms = 10, input_tokens = 1000, output_tokens = 500,
                        cost_usd = 0.0175, usage = AiUsageLedgerTests.UsageJson(),
                    })
                    : JsonContent.Create(new
                    {
                        score = 0.9, passed = true, rationale = "ok",
                        usage = AiUsageLedgerTests.UsageJson(),
                    }),
            };
        });
        var client = new EvalRunnerClient(http, recorder, ctx, TimeProvider.System);

        using (ctx.Begin(new AiUsageAttribution(org, user)))
        {
            await client.ExecutePromptAsync("p", "claude-opus-4-8", "in", null);
            await client.JudgeAsync("r", "i", "a", null, "claude-opus-4-8");
        }

        using var verify = _provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EvalDbContext>();
        var rows = await db.AiUsageRecords.ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(org, r.OrganizationId));
        Assert.All(rows, r => Assert.Equal(user, r.UserId));
        Assert.Contains(rows, r => r.Feature == AiUsageFeature.SubjectExecution);
        Assert.Contains(rows, r => r.Feature == AiUsageFeature.LlmJudge);
        Assert.All(rows, r => Assert.Equal(960, r.CacheReadTokens));

        // Cost snapshotted from the pricing table (6.1.T2): opus-4-8 with 1000 in / 500 out / 40
        // cache-write / 960 cache-read = 0.005 + 0.0125 + 0.00025 + 0.00048 = 0.01823.
        Assert.All(rows, r => Assert.Equal(0.01823m, r.CostUsd));
        Assert.All(rows, r => Assert.Equal("2026-07", r.RateVersion));
        Assert.All(rows, r => Assert.False(r.PricingMissing));
    }
}

public class ConfigUsagePricingTests
{
    private static readonly ConfigUsagePricing Pricing =
        new(Options.Create(AiUsagePricingOptions.Defaults()));

    [Fact]
    public void Compute_prices_input_output_and_cache_tokens()
    {
        // opus-4-8: 1e6 in ×5 + 1e6 out ×25 + 1e6 cache-write ×6.25 + 1e6 cache-read ×0.5 = 36.75
        var cost = Pricing.Compute("claude-opus-4-8", 1_000_000, 1_000_000, 1_000_000, 1_000_000);

        Assert.Equal(36.75m, cost.CostUsd);
        Assert.Equal("2026-07", cost.RateVersion);
        Assert.False(cost.PricingMissing);
    }

    [Fact]
    public void Compute_flags_an_unknown_model_with_null_cost_and_the_version()
    {
        var cost = Pricing.Compute("some-unpriced-model", 100, 100, 0, 0);

        Assert.Null(cost.CostUsd);
        Assert.True(cost.PricingMissing);
        Assert.Equal("2026-07", cost.RateVersion);
    }

    [Fact]
    public void Config_overrides_a_model_rate_and_the_version_over_the_defaults()
    {
        // Later price changes apply to new records only; historical rows keep their snapshot. Here we
        // prove config can override the table (the mechanism ops use to change rates going forward).
        var options = AiUsagePricingOptions.Defaults();
        options.RateVersion = "2026-08";
        options.Models["claude-opus-4-8"] = new AiUsagePricingOptions.ModelRate
        {
            InputPerMTokUsd = 4m, OutputPerMTokUsd = 20m, CacheWritePerMTokUsd = 5m, CacheReadPerMTokUsd = 0.4m,
        };
        var pricing = new ConfigUsagePricing(Options.Create(options));

        var cost = pricing.Compute("claude-opus-4-8", 1_000_000, 0, 0, 0);

        Assert.Equal(4m, cost.CostUsd);
        Assert.Equal("2026-08", cost.RateVersion);
    }
}
