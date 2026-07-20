namespace Domain;

/// <summary>Which kind of model call a ledger record is for (6.1).</summary>
public enum AiUsageFeature
{
    /// <summary>Running a prompt on its subject model.</summary>
    SubjectExecution,

    /// <summary>An LLM-judge scoring call.</summary>
    LlmJudge,

    /// <summary>A synthetic fixture-generation call.</summary>
    SyntheticGeneration,
}

/// <summary>The outcome of a model call — a failed call still incurs cost and signals waste (6.1).</summary>
public enum AiUsageStatus
{
    Success,
    Refusal,
    Error,
}

/// <summary>
/// One row of the AI-usage ledger (6.1): every model call the harness makes through the eval-runner
/// seam — subject execution, LLM-judge, and synthetic generation — recorded on success and failure.
/// Metadata + token counts only; never prompt/response content and never keys. Cost is snapshotted
/// separately (6.1.T2) so a later price change never rewrites history.
/// </summary>
public sealed class AiUsageRecord
{
    public Guid Id { get; private set; }

    /// <summary>The model as echoed by the runner (or the requested model on a pre-response failure).</summary>
    public string Model { get; private set; }

    public AiUsageFeature Feature { get; private set; }
    public AiUsageStatus Status { get; private set; }

    /// <summary>The owning organization; null when a call failed before attribution was known.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>The calling user; null when a call failed before attribution was known.</summary>
    public Guid? UserId { get; private set; }

    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int CacheCreationTokens { get; private set; }
    public int CacheReadTokens { get; private set; }

    public int LatencyMs { get; private set; }

    /// <summary>The max-tokens requested for the call, when known.</summary>
    public int? MaxTokens { get; private set; }

    /// <summary>The provider request id, when the response carried one.</summary>
    public string? RequestId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Cost of the call in USD, snapshotted at write time from the versioned pricing table (6.1.T2);
    /// null when the model has no pricing entry (see <see cref="PricingMissing"/>). Frozen once set so
    /// a later price change never rewrites history.
    /// </summary>
    public decimal? CostUsd { get; private set; }

    /// <summary>The pricing-table version the cost was computed against (6.1.T2).</summary>
    public string? RateVersion { get; private set; }

    /// <summary>True when the model had no pricing entry — tokens are still stored, cost is null.</summary>
    public bool PricingMissing { get; private set; }

    // Required by EF Core materialization; not for application use.
    private AiUsageRecord()
    {
        Model = string.Empty;
    }

    public static AiUsageRecord Create(
        string model,
        AiUsageFeature feature,
        AiUsageStatus status,
        Guid? organizationId,
        Guid? userId,
        int inputTokens,
        int outputTokens,
        int cacheCreationTokens,
        int cacheReadTokens,
        int latencyMs,
        int? maxTokens,
        string? requestId,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model must not be blank.", nameof(model));
        NonNegative(inputTokens, nameof(inputTokens));
        NonNegative(outputTokens, nameof(outputTokens));
        NonNegative(cacheCreationTokens, nameof(cacheCreationTokens));
        NonNegative(cacheReadTokens, nameof(cacheReadTokens));
        NonNegative(latencyMs, nameof(latencyMs));
        if (maxTokens is < 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), maxTokens, "Max tokens must be non-negative.");

        return new AiUsageRecord
        {
            Id = Guid.NewGuid(),
            Model = model,
            Feature = feature,
            Status = status,
            OrganizationId = organizationId,
            UserId = userId,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationTokens = cacheCreationTokens,
            CacheReadTokens = cacheReadTokens,
            LatencyMs = latencyMs,
            MaxTokens = maxTokens,
            RequestId = requestId,
            OccurredAt = occurredAt,
        };
    }

    /// <summary>
    /// Snapshots the cost + rate version onto the record (6.1.T2). Called once, at write time, from the
    /// versioned pricing table; the values are then frozen. <paramref name="costUsd"/> is null when the
    /// model has no pricing entry (<paramref name="pricingMissing"/> true), tokens still stored.
    /// </summary>
    public void ApplyCost(decimal? costUsd, string rateVersion, bool pricingMissing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rateVersion);
        if (costUsd is < 0)
            throw new ArgumentOutOfRangeException(nameof(costUsd), costUsd, "Cost must be non-negative.");

        CostUsd = costUsd;
        RateVersion = rateVersion;
        PricingMissing = pricingMissing;
    }

    private static void NonNegative(int value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be non-negative.");
    }
}
