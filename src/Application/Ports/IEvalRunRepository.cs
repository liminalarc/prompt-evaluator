using Domain;

namespace Application.Ports;

/// <summary>Persistence port for <see cref="EvalRun"/> aggregates. Runs are append-only.</summary>
public interface IEvalRunRepository
{
    Task AddAsync(EvalRun run, CancellationToken ct = default);

    Task<EvalRun?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>All runs over a dataset, newest first, with their fixture results and scores.</summary>
    Task<IReadOnlyList<EvalRun>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default);
}
