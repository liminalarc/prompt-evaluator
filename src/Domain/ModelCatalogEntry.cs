namespace Domain;

/// <summary>
/// One model in the workspace-wide Model Catalog (spec 1.13): the single source of truth for
/// "which models are available", their provider, and the roles they can serve. Drives the
/// target/judge droplists so people pick valid ids instead of typing them. The catalog is
/// additive — a legacy free-text model id on an existing prompt version still displays and runs;
/// enforcement of an unavailable model stays in the eval-runner (a clear 400).
///
/// Roles are stored as three boolean flags rather than a <c>List&lt;<see cref="ModelRole"/>&gt;</c>
/// (no such precedent in the schema; the set is small and fixed) and exposed via <see cref="Roles"/>.
/// Pricing here is <b>display-only</b> and, since 6.2, an <b>optional per-model override</b>: the
/// catalog's displayed price is <c>override ?? authoritative pricing-table rate</c> (the same
/// <c>AiUsagePricingOptions</c> the AI-usage ledger charges against, 6.1). The eval-runner remains the
/// execution pricing authority for per-run <c>FixtureRun</c> cost (1.5).
/// </summary>
public sealed class ModelCatalogEntry
{
    public Guid Id { get; private set; }

    /// <summary>The provider model id used at eval time, e.g. <c>gpt-4o-mini</c>. Immutable, unique.</summary>
    public string ModelId { get; private set; }

    public string DisplayName { get; private set; }
    public ModelProvider Provider { get; private set; }

    public bool CanSubject { get; private set; }
    public bool CanJudge { get; private set; }
    public bool CanGenerate { get; private set; }

    /// <summary>Display-only input price ($ per million tokens); null when unknown.</summary>
    public decimal? InputPricePerMTokUsd { get; private set; }

    /// <summary>Display-only output price ($ per million tokens); null when unknown.</summary>
    public decimal? OutputPricePerMTokUsd { get; private set; }

    /// <summary>Deactivated entries stay for history but are not offered in the droplists.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The roles this model can serve, derived from the flags (Subject → Judge → Generator).</summary>
    public IReadOnlyList<ModelRole> Roles
    {
        get
        {
            var roles = new List<ModelRole>();
            if (CanSubject) roles.Add(ModelRole.Subject);
            if (CanJudge) roles.Add(ModelRole.Judge);
            if (CanGenerate) roles.Add(ModelRole.Generator);
            return roles;
        }
    }

    private ModelCatalogEntry(
        Guid id, string modelId, string displayName, ModelProvider provider,
        bool canSubject, bool canJudge, bool canGenerate,
        decimal? inputPricePerMTokUsd, decimal? outputPricePerMTokUsd, bool isActive)
    {
        Id = id;
        ModelId = modelId;
        DisplayName = displayName;
        Provider = provider;
        CanSubject = canSubject;
        CanJudge = canJudge;
        CanGenerate = canGenerate;
        InputPricePerMTokUsd = inputPricePerMTokUsd;
        OutputPricePerMTokUsd = outputPricePerMTokUsd;
        IsActive = isActive;
    }

    // Required by EF Core materialization; not for application use.
    private ModelCatalogEntry()
    {
        ModelId = string.Empty;
        DisplayName = string.Empty;
    }

    public static ModelCatalogEntry Create(
        string modelId,
        string displayName,
        ModelProvider provider,
        IReadOnlyCollection<ModelRole> roles,
        decimal? inputPricePerMTokUsd = null,
        decimal? outputPricePerMTokUsd = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model id must not be blank.", nameof(modelId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be blank.", nameof(displayName));
        if (roles is null || roles.Count == 0)
            throw new ArgumentException("A model must serve at least one role.", nameof(roles));

        return new ModelCatalogEntry(
            Guid.NewGuid(), modelId.Trim(), displayName.Trim(), provider,
            roles.Contains(ModelRole.Subject), roles.Contains(ModelRole.Judge), roles.Contains(ModelRole.Generator),
            RequireNonNegative(inputPricePerMTokUsd, nameof(inputPricePerMTokUsd)),
            RequireNonNegative(outputPricePerMTokUsd, nameof(outputPricePerMTokUsd)),
            isActive: true);
    }

    /// <summary>
    /// Edits the mutable fields (admin management, 1.13). The <see cref="ModelId"/> is the model's
    /// identity and never changes; use <see cref="Deactivate"/> to retire an entry instead.
    /// </summary>
    public void Update(
        string displayName,
        ModelProvider provider,
        IReadOnlyCollection<ModelRole> roles,
        decimal? inputPricePerMTokUsd,
        decimal? outputPricePerMTokUsd)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be blank.", nameof(displayName));
        if (roles is null || roles.Count == 0)
            throw new ArgumentException("A model must serve at least one role.", nameof(roles));

        DisplayName = displayName.Trim();
        Provider = provider;
        CanSubject = roles.Contains(ModelRole.Subject);
        CanJudge = roles.Contains(ModelRole.Judge);
        CanGenerate = roles.Contains(ModelRole.Generator);
        InputPricePerMTokUsd = RequireNonNegative(inputPricePerMTokUsd, nameof(inputPricePerMTokUsd));
        OutputPricePerMTokUsd = RequireNonNegative(outputPricePerMTokUsd, nameof(outputPricePerMTokUsd));
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static decimal? RequireNonNegative(decimal? value, string paramName)
    {
        if (value is < 0)
            throw new ArgumentException("Price must not be negative.", paramName);
        return value;
    }
}
