using Application.AiUsage;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

/// <summary>
/// 6.1.T3: the AI-usage read model — filter, aggregate (metrics incl. p50/p95), breakdown, time series,
/// and paginated calls — verified against a seeded, known dataset.
/// </summary>
public sealed class AiUsageQueryServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private EvalDbContext _db = null!;
    private AiUsageQueryService _queries = null!;

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid User1 = Guid.NewGuid();
    private static readonly Guid User2 = Guid.NewGuid();
    private static readonly DateTimeOffset Day1 = new(2026, 7, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 7, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day3 = new(2026, 7, 3, 8, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<EvalDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new EvalDbContext(options);
        await _db.Database.MigrateAsync();
        _queries = new AiUsageQueryService(_db);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        void Add(AiUsageFeature feature, Guid org, Guid user, AiUsageStatus status, int latency, decimal cost, DateTimeOffset at)
        {
            var r = AiUsageRecord.Create(
                "claude-opus-4-8", feature, status, org, user,
                inputTokens: 100, outputTokens: 50, cacheCreationTokens: 10, cacheReadTokens: 20,
                latencyMs: latency, maxTokens: 4096, requestId: "req", occurredAt: at);
            r.ApplyCost(cost, "2026-07", pricingMissing: false);
            _db.AiUsageRecords.Add(r);
        }

        Add(AiUsageFeature.SubjectExecution, OrgA, User1, AiUsageStatus.Success, 10, 0.001m, Day1);
        Add(AiUsageFeature.LlmJudge, OrgA, User1, AiUsageStatus.Success, 20, 0.002m, Day1);
        Add(AiUsageFeature.SyntheticGeneration, OrgB, User2, AiUsageStatus.Success, 30, 0.003m, Day2);
        Add(AiUsageFeature.SubjectExecution, OrgA, User2, AiUsageStatus.Error, 40, 0.004m, Day2);
        Add(AiUsageFeature.LlmJudge, OrgB, User1, AiUsageStatus.Success, 50, 0.005m, Day3);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Summary_computes_totals_averages_success_rate_and_percentiles()
    {
        var m = await _queries.SummaryAsync(new AiUsageFilter());

        Assert.Equal(5, m.CallCount);
        Assert.Equal(0.015m, m.TotalCostUsd);
        Assert.Equal(500, m.InputTokens);
        Assert.Equal(250, m.OutputTokens);
        Assert.Equal(50, m.CacheCreationTokens);
        Assert.Equal(100, m.CacheReadTokens);
        Assert.Equal(0.003m, m.AvgCostPerCall);
        Assert.Equal(180, m.AvgTokensPerCall);          // (500+250+50+100)/5
        Assert.Equal(0.8, m.SuccessRate, 3);            // 4 of 5 success
        Assert.Equal(30, m.LatencyP50Ms);               // nearest-rank of [10,20,30,40,50]
        Assert.Equal(50, m.LatencyP95Ms);
    }

    [Fact]
    public async Task Filter_by_feature_and_status_narrows_the_set()
    {
        var subjectOnly = await _queries.SummaryAsync(
            new AiUsageFilter(Features: [AiUsageFeature.SubjectExecution]));
        Assert.Equal(2, subjectOnly.CallCount);
        Assert.Equal(0.005m, subjectOnly.TotalCostUsd);
        Assert.Equal(0.5, subjectOnly.SuccessRate, 3);  // one success, one error

        var errorsOnly = await _queries.SummaryAsync(new AiUsageFilter(Statuses: [AiUsageStatus.Error]));
        Assert.Equal(1, errorsOnly.CallCount);
    }

    [Fact]
    public async Task Filter_by_org_user_and_date_range_combine()
    {
        var orgAUser1 = await _queries.SummaryAsync(
            new AiUsageFilter(Organizations: [OrgA], Users: [User1]));
        Assert.Equal(2, orgAUser1.CallCount);

        var day2Plus = await _queries.SummaryAsync(new AiUsageFilter(From: Day2));
        Assert.Equal(3, day2Plus.CallCount);            // Day2 (2) + Day3 (1)

        var justDay1 = await _queries.SummaryAsync(
            new AiUsageFilter(From: Day1, To: Day1.AddHours(1)));
        Assert.Equal(2, justDay1.CallCount);
    }

    [Fact]
    public async Task Breakdown_by_feature_orders_by_cost_and_honors_top_n()
    {
        var rows = await _queries.BreakdownAsync(new AiUsageFilter(), AiUsageDimension.Feature);
        Assert.Equal(3, rows.Count);
        Assert.Equal("LlmJudge", rows[0].Key);          // 0.002 + 0.005 = 0.007, highest
        Assert.Equal(0.007m, rows[0].Metrics.TotalCostUsd);
        Assert.Equal("SubjectExecution", rows[1].Key);  // 0.005
        Assert.Equal("SyntheticGeneration", rows[2].Key); // 0.003

        var topTwo = await _queries.BreakdownAsync(new AiUsageFilter(), AiUsageDimension.Feature, topN: 2);
        Assert.Equal(2, topTwo.Count);
    }

    [Fact]
    public async Task Breakdown_by_org_groups_on_organization_id()
    {
        var rows = await _queries.BreakdownAsync(new AiUsageFilter(), AiUsageDimension.Organization);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Key == OrgA.ToString() && r.Metrics.CallCount == 3);
        Assert.Contains(rows, r => r.Key == OrgB.ToString() && r.Metrics.CallCount == 2);
    }

    [Fact]
    public async Task TimeSeries_by_day_buckets_by_date()
    {
        var points = await _queries.TimeSeriesAsync(new AiUsageFilter(), AiUsagePeriod.Day);

        Assert.Equal(3, points.Count);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), points[0].PeriodStart);
        Assert.Equal(2, points[0].Metrics.CallCount);
        Assert.Equal(1, points[2].Metrics.CallCount);
    }

    [Fact]
    public async Task Calls_paginates_newest_first()
    {
        var page1 = await _queries.CallsAsync(new AiUsageFilter(), page: 1, pageSize: 2);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.Items[0].OccurredAt >= page1.Items[1].OccurredAt);
        Assert.Equal(Day3, page1.Items[0].OccurredAt);  // newest first

        var page3 = await _queries.CallsAsync(new AiUsageFilter(), page: 3, pageSize: 2);
        Assert.Single(page3.Items);
    }

    [Fact]
    public async Task Export_returns_all_filtered_calls()
    {
        var all = await _queries.ExportAsync(new AiUsageFilter());
        Assert.Equal(5, all.Count);

        var judged = await _queries.ExportAsync(new AiUsageFilter(Features: [AiUsageFeature.LlmJudge]));
        Assert.Equal(2, judged.Count);
    }
}
