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

    /// <summary>
    /// This scorer's relative weight within the dataset's weighted composite score (2.9). Defaults to
    /// <c>1.0</c> so a new scorer joins equally weighted; the composite renormalizes over the weights of
    /// the scorers actually present. Deliberately <b>not</b> part of <see cref="ScorerDescriptor.Identity"/>
    /// — changing a weight re-weights the composite but never forks a scorer's score series.
    /// </summary>
    public double Weight { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ScorerConfig(Guid id, Guid datasetId, ScorerDescriptor scorer, double weight, DateTimeOffset createdAt)
    {
        Id = id;
        DatasetId = datasetId;
        Scorer = scorer;
        Weight = weight;
        CreatedAt = createdAt;
    }

    // Required by EF Core materialization; not for application use.
    private ScorerConfig()
    {
        Scorer = null!;
    }

    public static ScorerConfig Create(
        Guid datasetId, ScorerDescriptor scorer, DateTimeOffset createdAt, double weight = 1.0)
    {
        if (datasetId == Guid.Empty)
            throw new ArgumentException("Dataset id must not be empty.", nameof(datasetId));
        ArgumentNullException.ThrowIfNull(scorer);
        ValidateWeight(weight);

        return new ScorerConfig(Guid.NewGuid(), datasetId, scorer, weight, createdAt);
    }

    /// <summary>Sets the scorer's composite weight (2.9). Must be finite and strictly positive.</summary>
    public void SetWeight(double weight)
    {
        ValidateWeight(weight);
        Weight = weight;
    }

    private static void ValidateWeight(double weight)
    {
        if (!double.IsFinite(weight) || weight <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(weight), weight, "Scorer weight must be a finite, strictly positive number.");
    }

    /// <summary>
    /// Replaces the scorer descriptor (U9 — edit a configured scorer in place). The descriptor is
    /// the scorer's identity, so changing it starts a new score series for future runs; runs
    /// already recorded keep their own scorer identity (append-only history is untouched).
    /// </summary>
    public void Reconfigure(ScorerDescriptor scorer)
    {
        ArgumentNullException.ThrowIfNull(scorer);
        Scorer = scorer;
    }
}
