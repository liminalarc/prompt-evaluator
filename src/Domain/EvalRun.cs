namespace Domain;

/// <summary>
/// A single evaluation of a prompt version over a dataset — the aggregate at the centre of the
/// harness. Its identity is <c>Prompt × Version × Dataset</c>; it owns one <see cref="FixtureRun"/>
/// per fixture, each owning one <see cref="Score"/> per scorer. Runs are append-only: every run is
/// persisted and nothing is ever overwritten, so a version's scores can be compared to another's
/// over the same dataset (regression detection is 1.4).
/// </summary>
public sealed class EvalRun
{
    private readonly List<FixtureRun> _results = new();

    public Guid Id { get; private set; }
    public Guid PromptId { get; private set; }
    public Guid PromptVersionId { get; private set; }
    public Guid DatasetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The per-fixture results. Append-only from the outside.</summary>
    public IReadOnlyList<FixtureRun> Results => _results.AsReadOnly();

    private EvalRun(Guid id, Guid promptId, Guid promptVersionId, Guid datasetId, DateTimeOffset createdAt)
    {
        Id = id;
        PromptId = promptId;
        PromptVersionId = promptVersionId;
        DatasetId = datasetId;
        CreatedAt = createdAt;
    }

    // Required by EF Core materialization; not for application use.
    private EvalRun()
    {
    }

    public static EvalRun Create(Guid promptId, Guid promptVersionId, Guid datasetId, DateTimeOffset createdAt)
    {
        Require(promptId, nameof(promptId));
        Require(promptVersionId, nameof(promptVersionId));
        Require(datasetId, nameof(datasetId));

        return new EvalRun(Guid.NewGuid(), promptId, promptVersionId, datasetId, createdAt);
    }

    /// <summary>
    /// Records a fixture's execution — the captured model output plus its latency, token counts, and
    /// cost — and returns the <see cref="FixtureRun"/> so the caller can attach scores. Output may be
    /// empty; cost may be null. Latency, token counts, and cost must be non-negative.
    /// </summary>
    public FixtureRun RecordFixture(
        Guid fixtureId, string modelOutput, int latencyMs, int inputTokens, int outputTokens, decimal? costUsd)
    {
        Require(fixtureId, nameof(fixtureId));
        ArgumentNullException.ThrowIfNull(modelOutput);
        if (latencyMs < 0)
            throw new ArgumentOutOfRangeException(nameof(latencyMs), latencyMs, "Latency must be non-negative.");
        if (inputTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(inputTokens), inputTokens, "Input tokens must be non-negative.");
        if (outputTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(outputTokens), outputTokens, "Output tokens must be non-negative.");
        if (costUsd is < 0)
            throw new ArgumentOutOfRangeException(nameof(costUsd), costUsd, "Cost must be non-negative.");

        var result = new FixtureRun(fixtureId, modelOutput, latencyMs, inputTokens, outputTokens, costUsd);
        _results.Add(result);
        return result;
    }

    private static void Require(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException($"{name} must not be empty.", name);
    }
}
