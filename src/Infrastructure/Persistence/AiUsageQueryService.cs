using Application.AiUsage;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// EF-backed <see cref="IAiUsageQueries"/> (6.1.T3). Filtering runs in SQL; per-slice metrics
/// (incl. latency percentiles) are computed over the filtered set in memory — fine for the ledger
/// sizes this admin tool targets. The calls table paginates in SQL. Read-only.
/// </summary>
public sealed class AiUsageQueryService(EvalDbContext db) : IAiUsageQueries
{
    private const int ExportCap = 100_000;

    private IQueryable<AiUsageRecord> Filtered(AiUsageFilter f)
    {
        var q = db.AiUsageRecords.AsQueryable();
        if (f.From is { } from) q = q.Where(r => r.OccurredAt >= from);
        if (f.To is { } to) q = q.Where(r => r.OccurredAt <= to);
        if (f.Models is { Count: > 0 } models) q = q.Where(r => models.Contains(r.Model));
        if (f.Features is { Count: > 0 } features) q = q.Where(r => features.Contains(r.Feature));
        if (f.Users is { Count: > 0 } users) q = q.Where(r => r.UserId != null && users.Contains(r.UserId.Value));
        if (f.Organizations is { Count: > 0 } orgs) q = q.Where(r => r.OrganizationId != null && orgs.Contains(r.OrganizationId.Value));
        if (f.Statuses is { Count: > 0 } statuses) q = q.Where(r => statuses.Contains(r.Status));
        return q;
    }

    public async Task<AiUsageMetrics> SummaryAsync(AiUsageFilter filter, CancellationToken ct = default)
        => Metrics(await Filtered(filter).ToListAsync(ct));

    public async Task<IReadOnlyList<AiUsageBreakdownRow>> BreakdownAsync(
        AiUsageFilter filter, AiUsageDimension dimension, int? topN = null, CancellationToken ct = default)
    {
        var rows = await Filtered(filter).ToListAsync(ct);
        Func<AiUsageRecord, string> key = dimension switch
        {
            AiUsageDimension.Model => r => r.Model,
            AiUsageDimension.Feature => r => r.Feature.ToString(),
            AiUsageDimension.User => r => r.UserId?.ToString() ?? "unattributed",
            AiUsageDimension.Organization => r => r.OrganizationId?.ToString() ?? "unattributed",
            _ => r => r.Model,
        };

        var result = rows
            .GroupBy(key)
            .Select(g => new AiUsageBreakdownRow(g.Key, Metrics(g.ToList())))
            .OrderByDescending(r => r.Metrics.TotalCostUsd)
            .ThenByDescending(r => r.Metrics.CallCount)
            .ToList();

        return topN is { } n && n > 0 ? result.Take(n).ToList() : result;
    }

    public async Task<IReadOnlyList<AiUsageTimePoint>> TimeSeriesAsync(
        AiUsageFilter filter, AiUsagePeriod period, CancellationToken ct = default)
    {
        var rows = await Filtered(filter).ToListAsync(ct);
        return rows
            .GroupBy(r => Bucket(r.OccurredAt, period))
            .OrderBy(g => g.Key)
            .Select(g => new AiUsageTimePoint(g.Key, Metrics(g.ToList())))
            .ToList();
    }

    public async Task<AiUsageCallsPage> CallsAsync(
        AiUsageFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var ordered = Filtered(filter).OrderByDescending(r => r.OccurredAt).ThenBy(r => r.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToCall)
            .ToListAsync(ct);
        return new AiUsageCallsPage(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<AiUsageCall>> ExportAsync(AiUsageFilter filter, CancellationToken ct = default)
        => await Filtered(filter)
            .OrderByDescending(r => r.OccurredAt)
            .ThenBy(r => r.Id)
            .Take(ExportCap)
            .Select(ToCall)
            .ToListAsync(ct);

    // Projection EF can translate directly into the DTO.
    private static readonly System.Linq.Expressions.Expression<Func<AiUsageRecord, AiUsageCall>> ToCall =
        r => new AiUsageCall(
            r.Id, r.OccurredAt, r.Feature, r.Model,
            r.InputTokens, r.OutputTokens, r.CacheCreationTokens, r.CacheReadTokens,
            r.CostUsd, r.OrganizationId, r.UserId, r.Status, r.LatencyMs, r.RequestId);

    private static AiUsageMetrics Metrics(IReadOnlyList<AiUsageRecord> rows)
    {
        if (rows.Count == 0)
            return AiUsageMetrics.Empty;

        var count = rows.Count;
        var totalCost = rows.Sum(r => r.CostUsd ?? 0m);
        long input = rows.Sum(r => (long)r.InputTokens);
        long output = rows.Sum(r => (long)r.OutputTokens);
        long cacheCreate = rows.Sum(r => (long)r.CacheCreationTokens);
        long cacheRead = rows.Sum(r => (long)r.CacheReadTokens);
        var totalTokens = input + output + cacheCreate + cacheRead;
        var successes = rows.Count(r => r.Status == AiUsageStatus.Success);
        var latencies = rows.Select(r => r.LatencyMs).OrderBy(x => x).ToArray();

        return new AiUsageMetrics(
            TotalCostUsd: decimal.Round(totalCost, 6),
            InputTokens: input,
            OutputTokens: output,
            CacheCreationTokens: cacheCreate,
            CacheReadTokens: cacheRead,
            CallCount: count,
            AvgCostPerCall: decimal.Round(totalCost / count, 6),
            AvgTokensPerCall: (double)totalTokens / count,
            SuccessRate: (double)successes / count,
            LatencyP50Ms: Percentile(latencies, 50),
            LatencyP95Ms: Percentile(latencies, 95));
    }

    // Nearest-rank percentile over an ascending-sorted array.
    private static int Percentile(int[] sorted, int p)
    {
        if (sorted.Length == 0)
            return 0;
        var rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    private static DateTimeOffset Bucket(DateTimeOffset at, AiUsagePeriod period)
    {
        var date = at.UtcDateTime.Date;
        return period switch
        {
            AiUsagePeriod.Day => new DateTimeOffset(date, TimeSpan.Zero),
            AiUsagePeriod.Week => new DateTimeOffset(date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), TimeSpan.Zero),
            AiUsagePeriod.Month => new DateTimeOffset(new DateTime(date.Year, date.Month, 1), TimeSpan.Zero),
            _ => new DateTimeOffset(date, TimeSpan.Zero),
        };
    }
}
