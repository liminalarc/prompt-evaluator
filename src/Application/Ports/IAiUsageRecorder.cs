using Domain;

namespace Application.Ports;

/// <summary>
/// Persists AI-usage ledger records (6.1). Implemented in Infrastructure over its own unit of work,
/// so a record survives even when the surrounding eval operation later fails.
/// </summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(AiUsageRecord record, CancellationToken ct = default);
}
