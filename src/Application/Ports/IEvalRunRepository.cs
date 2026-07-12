using Domain;

namespace Application.Ports;

/// <summary>Persistence port for <see cref="EvalRun"/> aggregates. Runs are append-only.</summary>
public interface IEvalRunRepository
{
    Task AddAsync(EvalRun run, CancellationToken ct = default);

    Task<EvalRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
