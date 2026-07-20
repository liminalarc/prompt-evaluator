using System.Net;
using System.Net.Http.Json;
using Domain;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// 6.1.T3/T4: the Admin → AI Usage endpoints — filter / aggregate / calls / CSV — and the global-admin
/// gate (non-admin → 403, no data leak).
/// </summary>
public sealed class AiUsageEndpointTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@test.local";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid User1 = Guid.NewGuid();
    private static readonly DateTimeOffset When = new(2026, 7, 10, 8, 0, 0, TimeSpan.Zero);

    private sealed class AdminFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.UseSetting("Auth:BootstrapAdmin:Email", AdminEmail);
            builder.UseSetting("Auth:BootstrapAdmin:Password", AuthenticationTestExtensions.DefaultPassword);
            builder.UseSetting("Auth:BootstrapAdmin:DisplayName", "Admin");
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new AdminFactory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = AuthenticationTestExtensions.DefaultPassword });
        res.EnsureSuccessStatusCode();
        return client;
    }

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EvalDbContext>();

        void Add(AiUsageFeature feature, AiUsageStatus status, decimal cost, int latency)
        {
            var r = AiUsageRecord.Create(
                "claude-opus-4-8", feature, status, OrgA, User1,
                inputTokens: 100, outputTokens: 50, cacheCreationTokens: 0, cacheReadTokens: 0,
                latencyMs: latency, maxTokens: 4096, requestId: "req", occurredAt: When);
            r.ApplyCost(cost, "2026-07", pricingMissing: false);
            db.AiUsageRecords.Add(r);
        }

        Add(AiUsageFeature.SubjectExecution, AiUsageStatus.Success, 0.001m, 10);
        Add(AiUsageFeature.LlmJudge, AiUsageStatus.Success, 0.002m, 20);
        Add(AiUsageFeature.SubjectExecution, AiUsageStatus.Error, 0.004m, 30);
        db.SaveChanges();
    }

    private sealed record SummaryDto(int CallCount, decimal TotalCostUsd, double SuccessRate);
    private sealed record BreakdownRowDto(string Key, SummaryDto Metrics);
    private sealed record CallsPageDto(int TotalCount, int Page, List<System.Text.Json.JsonElement> Items);

    [Fact]
    public async Task Admin_gets_a_summary_and_can_filter_by_feature()
    {
        var admin = await AdminClientAsync();
        Seed();

        var all = (await admin.GetFromJsonAsync<SummaryDto>("/api/admin/ai-usage/summary"))!;
        Assert.Equal(3, all.CallCount);
        Assert.Equal(0.007m, all.TotalCostUsd);

        var subject = (await admin.GetFromJsonAsync<SummaryDto>(
            "/api/admin/ai-usage/summary?features=SubjectExecution"))!;
        Assert.Equal(2, subject.CallCount);
        Assert.Equal(0.005m, subject.TotalCostUsd);
    }

    [Fact]
    public async Task Admin_gets_a_breakdown_by_feature()
    {
        var admin = await AdminClientAsync();
        Seed();

        var rows = (await admin.GetFromJsonAsync<List<BreakdownRowDto>>(
            "/api/admin/ai-usage/breakdown?dimension=feature"))!;

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Key == "SubjectExecution" && r.Metrics.CallCount == 2);
        Assert.Contains(rows, r => r.Key == "LlmJudge" && r.Metrics.CallCount == 1);
    }

    [Fact]
    public async Task Admin_gets_paginated_calls()
    {
        var admin = await AdminClientAsync();
        Seed();

        var page = (await admin.GetFromJsonAsync<CallsPageDto>(
            "/api/admin/ai-usage/calls?page=1&pageSize=2"))!;

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Admin_exports_filtered_calls_as_csv()
    {
        var admin = await AdminClientAsync();
        Seed();

        var res = await admin.GetAsync("/api/admin/ai-usage/export.csv");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType!.MediaType);
        var csv = await res.Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("occurred_at,feature,model,status", lines[0]);
        Assert.Equal(4, lines.Length); // header + 3 rows
        Assert.Contains("claude-opus-4-8", csv);
    }

    [Fact]
    public async Task Bad_dimension_is_a_400()
    {
        var admin = await AdminClientAsync();

        var res = await admin.GetAsync("/api/admin/ai-usage/breakdown?dimension=nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/ai-usage/summary")]
    [InlineData("/api/admin/ai-usage/breakdown?dimension=feature")]
    [InlineData("/api/admin/ai-usage/timeseries?period=day")]
    [InlineData("/api/admin/ai-usage/calls")]
    [InlineData("/api/admin/ai-usage/export.csv")]
    public async Task Non_admin_gets_403_and_no_data_from_every_ai_usage_endpoint(string path)
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");
        Seed();

        var res = await member.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("claude-opus-4-8", body); // no ledger data leaked
    }

    [Fact]
    public async Task Anonymous_is_challenged()
    {
        var res = await _factory.CreateClient().GetAsync("/api/admin/ai-usage/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
