namespace Domain;

/// <summary>
/// A scorer selected for a dataset. Persisted per dataset (not per run) so every
/// <see cref="EvalRun"/> over that dataset scores with the same set — the stable foundation
/// regression detection (1.4) needs. The <see cref="Scorer"/> value object carries the full
/// identity (kind, config, and — for LLM-judge — the judge model).
/// </summary>
public sealed class ScorerConfig
{
    public Guid Id { get; private set; }
    public Guid DatasetId { get; private set; }
    public ScorerDescriptor Scorer { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ScorerConfig(Guid id, Guid datasetId, ScorerDescriptor scorer, DateTimeOffset createdAt)
    {
        Id = id;
        DatasetId = datasetId;
        Scorer = scorer;
        CreatedAt = createdAt;
    }

    // Required by EF Core materialization; not for application use.
    private ScorerConfig()
    {
        Scorer = null!;
    }

    public static ScorerConfig Create(Guid datasetId, ScorerDescriptor scorer, DateTimeOffset createdAt)
    {
        if (datasetId == Guid.Empty)
            throw new ArgumentException("Dataset id must not be empty.", nameof(datasetId));
        ArgumentNullException.ThrowIfNull(scorer);

        return new ScorerConfig(Guid.NewGuid(), datasetId, scorer, createdAt);
    }
}
