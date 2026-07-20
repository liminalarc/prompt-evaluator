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
    // Seed within the current month so budget status (which keys off the real clock) sees the spend.
    private static readonly DateTimeOffset When = DateTimeOffset.UtcNow;

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

    // ---- Budgets (T6) ----

    private sealed record BudgetDto(Guid Id, string Scope, string? ScopeValue, decimal LimitUsd, int AlertThresholdPercent);
    private sealed record BudgetStatusDto(BudgetDto Budget, decimal SpendUsd, double PercentUsed, string Level);

    [Fact]
    public async Task Admin_creates_a_global_and_a_scoped_budget_that_appear_in_the_list()
    {
        var admin = await AdminClientAsync();

        var global = await admin.PostAsJsonAsync("/api/admin/ai-usage/budgets",
            new { scope = "Global", limitUsd = 100m, alertThresholdPercent = 80 });
        Assert.Equal(HttpStatusCode.Created, global.StatusCode);

        var scoped = await admin.PostAsJsonAsync("/api/admin/ai-usage/budgets",
            new { scope = "Model", scopeValue = "claude-opus-4-8", limitUsd = 25m, alertThresholdPercent = 90 });
        Assert.Equal(HttpStatusCode.Created, scoped.StatusCode);

        var list = (await admin.GetFromJsonAsync<List<BudgetDto>>("/api/admin/ai-usage/budgets"))!;
        Assert.Equal(2, list.Count);
        Assert.Contains(list, b => b.Scope == "Global" && b.LimitUsd == 100m);
        Assert.Contains(list, b => b.Scope == "Model" && b.ScopeValue == "claude-opus-4-8");
    }

    [Fact]
    public async Task Budget_status_reports_over_threshold_when_spend_exceeds_the_limit()
    {
        var admin = await AdminClientAsync();
        Seed(); // total spend 0.007

        await admin.PostAsJsonAsync("/api/admin/ai-usage/budgets",
            new { scope = "Global", limitUsd = 0.005m, alertThresholdPercent = 80 });

        var statuses = (await admin.GetFromJsonAsync<List<BudgetStatusDto>>("/api/admin/ai-usage/budgets/status"))!;

        var global = Assert.Single(statuses);
        Assert.Equal(0.007m, global.SpendUsd);
        Assert.Equal("Over", global.Level);
    }

    [Fact]
    public async Task Admin_deletes_a_budget()
    {
        var admin = await AdminClientAsync();
        var created = await admin.PostAsJsonAsync("/api/admin/ai-usage/budgets",
            new { scope = "Global", limitUsd = 10m });
        var budget = (await created.Content.ReadFromJsonAsync<BudgetDto>())!;

        var del = await admin.DeleteAsync($"/api/admin/ai-usage/budgets/{budget.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<BudgetDto>>("/api/admin/ai-usage/budgets"))!);
    }

    [Fact]
    public async Task Creating_a_budget_with_a_nonpositive_limit_is_400()
    {
        var admin = await AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/admin/ai-usage/budgets",
            new { scope = "Global", limitUsd = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_read_or_create_budgets()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/ai-usage/budgets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/ai-usage/budgets/status")).StatusCode);
        var create = await member.PostAsJsonAsync("/api/admin/ai-usage/budgets", new { scope = "Global", limitUsd = 10m });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }
}
