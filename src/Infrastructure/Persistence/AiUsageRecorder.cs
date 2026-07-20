using Application.Ports;
using Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

// (IUsagePricing lives in Application.Ports.)

/// <summary>
/// EF-backed <see cref="IAiUsageRecorder"/> (6.1). Each record is written on its <em>own</em> unit of
/// work — a fresh <see cref="EvalDbContext"/> resolved from a new scope — so a ledger row persists even
/// when the surrounding eval operation later throws (a failed call still signals cost/waste), and so a
/// mid-run write never flushes the caller's half-built aggregate.
/// </summary>
public sealed class AiUsageRecorder(IServiceScopeFactory scopeFactory, IUsagePricing pricing) : IAiUsageRecorder
{
    public async Task RecordAsync(AiUsageRecord record, CancellationToken ct = default)
    {
        // Snapshot the cost at write time from the versioned pricing table (6.1.T2) — frozen on the
        // record so a later price change never rewrites history.
        var cost = pricing.Compute(
            record.Model, record.InputTokens, record.OutputTokens,
            record.CacheCreationTokens, record.CacheReadTokens);
        record.ApplyCost(cost.CostUsd, cost.RateVersion, cost.PricingMissing);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EvalDbContext>();
        db.Set<AiUsageRecord>().Add(record);
        await db.SaveChangesAsync(ct);
    }
}
