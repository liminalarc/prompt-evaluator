using Domain;

namespace Application.AiUsage;

/// <summary>How to group an AI-usage breakdown (6.1.T3).</summary>
public enum AiUsageDimension { Model, Feature, User, Organization }

/// <summary>Period bucket for the AI-usage time series (6.1.T3).</summary>
public enum AiUsagePeriod { Day, Week, Month }

/// <summary>
/// Filter over the AI-usage ledger (6.1.T3). Any combination of dimensions; an empty/omitted list
/// means "no filter on that dimension". <see cref="From"/>/<see cref="To"/> are inclusive UTC bounds.
/// </summary>
public sealed record AiUsageFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    IReadOnlyList<string>? Models = null,
    IReadOnlyList<AiUsageFeature>? Features = null,
    IReadOnlyList<Guid>? Users = null,
    IReadOnlyList<Guid>? Organizations = null,
    IReadOnlyList<AiUsageStatus>? Statuses = null);

/// <summary>Per-slice metrics (6.1.T3).</summary>
public sealed record AiUsageMetrics(
    decimal TotalCostUsd,
    long InputTokens,
    long OutputTokens,
    long CacheCreationTokens,
    long CacheReadTokens,
    int CallCount,
    decimal AvgCostPerCall,
    double AvgTokensPerCall,
    double SuccessRate,
    int LatencyP50Ms,
    int LatencyP95Ms)
{
    public static readonly AiUsageMetrics Empty = new(0m, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0);
}

/// <summary>One breakdown row — a dimension key (model id / feature / user or org id) + its metrics.</summary>
public sealed record AiUsageBreakdownRow(string Key, AiUsageMetrics Metrics);

/// <summary>One point on the spend-over-time series.</summary>
public sealed record AiUsageTimePoint(DateTimeOffset PeriodStart, AiUsageMetrics Metrics);

/// <summary>One individual call in the drill-down table (metadata only — no prompt/response content).</summary>
public sealed record AiUsageCall(
    Guid Id,
    DateTimeOffset OccurredAt,
    AiUsageFeature Feature,
    string Model,
    int InputTokens,
    int OutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    decimal? CostUsd,
    Guid? OrganizationId,
    Guid? UserId,
    AiUsageStatus Status,
    int LatencyMs,
    string? RequestId);

/// <summary>A page of the calls table.</summary>
public sealed record AiUsageCallsPage(IReadOnlyList<AiUsageCall> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Read model over the AI-usage ledger (6.1.T3): filter + aggregate + drill + export. Read-only — no
/// domain mutation. Percentiles are computed over the filtered set.
/// </summary>
public interface IAiUsageQueries
{
    Task<AiUsageMetrics> SummaryAsync(AiUsageFilter filter, CancellationToken ct = default);

    Task<IReadOnlyList<AiUsageBreakdownRow>> BreakdownAsync(
        AiUsageFilter filter, AiUsageDimension dimension, int? topN = null, CancellationToken ct = default);

    Task<IReadOnlyList<AiUsageTimePoint>> TimeSeriesAsync(
        AiUsageFilter filter, AiUsagePeriod period, CancellationToken ct = default);

    Task<AiUsageCallsPage> CallsAsync(
        AiUsageFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>All filtered calls for CSV export (capped defensively for very large ledgers).</summary>
    Task<IReadOnlyList<AiUsageCall>> ExportAsync(AiUsageFilter filter, CancellationToken ct = default);
}
