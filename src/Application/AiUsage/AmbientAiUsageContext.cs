using Application.Ports;

namespace Application.AiUsage;

/// <summary>
/// <see cref="IAiUsageContextAccessor"/> backed by an <see cref="AsyncLocal{T}"/>, so the attribution
/// a handler sets flows across every awaited eval-runner call in the same async operation — including
/// the judge call that runs indirectly through <c>IScorer</c>. Pure (no external dependencies); a
/// single instance is shared. Registered in composition.
/// </summary>
public sealed class AmbientAiUsageContext : IAiUsageContextAccessor
{
    private static readonly AsyncLocal<AiUsageAttribution> Slot = new();

    public AiUsageAttribution Current => Slot.Value;

    public IDisposable Begin(AiUsageAttribution attribution)
    {
        var previous = Slot.Value;
        Slot.Value = attribution;
        return new Scope(previous);
    }

    private sealed class Scope(AiUsageAttribution previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Slot.Value = previous;
        }
    }
}
