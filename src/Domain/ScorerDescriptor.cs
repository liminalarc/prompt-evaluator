using System.Security.Cryptography;
using System.Text;

namespace Domain;

/// <summary>
/// The immutable identity of a scorer — the "Scorer" in <c>Prompt × Version × Dataset × Scorer</c>.
/// It is the <see cref="Kind"/>, its canonical <see cref="Config"/> (regex pattern, JSON schema,
/// fuzzy threshold, or LLM rubric), and — for <see cref="ScorerKind.LlmJudge"/> — the
/// <see cref="JudgeModel"/>. The judge model is part of the identity on purpose: changing it
/// yields a distinct <see cref="Identity"/> and therefore a distinct score series, so regression
/// detection (1.4) only ever compares like-for-like.
/// </summary>
public sealed record ScorerDescriptor
{
    public ScorerKind Kind { get; }

    /// <summary>Canonical configuration for the scorer; empty string when the kind needs none.</summary>
    public string Config { get; }

    /// <summary>The judge's Claude model id — set only for <see cref="ScorerKind.LlmJudge"/>.</summary>
    public string? JudgeModel { get; }

    private ScorerDescriptor(ScorerKind kind, string config, string? judgeModel)
    {
        Kind = kind;
        Config = config;
        JudgeModel = judgeModel;
    }

    // Required by EF Core materialization; not for application use.
    private ScorerDescriptor()
    {
        Config = string.Empty;
    }

    /// <summary>
    /// Builds a deterministic (in-process) scorer descriptor. <see cref="ScorerKind.Regex"/> and
    /// <see cref="ScorerKind.JsonSchema"/> require a <paramref name="config"/> (the pattern / schema);
    /// the others treat it as optional (e.g. a fuzzy-match threshold or a latency/cost budget).
    /// </summary>
    public static ScorerDescriptor Deterministic(ScorerKind kind, string? config = null)
    {
        if (kind == ScorerKind.LlmJudge)
            throw new ArgumentException("Use LlmJudge(...) for judge scorers.", nameof(kind));

        var normalized = config?.Trim() ?? string.Empty;
        if ((kind is ScorerKind.Regex or ScorerKind.JsonSchema) && normalized.Length == 0)
            throw new ArgumentException($"Scorer '{kind}' requires a config.", nameof(config));

        return new ScorerDescriptor(kind, normalized, judgeModel: null);
    }

    /// <summary>
    /// Builds an LLM-judge scorer descriptor. Both the <paramref name="rubric"/> and the
    /// <paramref name="judgeModel"/> are required and both are part of the scorer's identity.
    /// </summary>
    public static ScorerDescriptor LlmJudge(string rubric, string judgeModel)
    {
        if (string.IsNullOrWhiteSpace(rubric))
            throw new ArgumentException("An LLM-judge scorer requires a rubric.", nameof(rubric));
        if (string.IsNullOrWhiteSpace(judgeModel))
            throw new ArgumentException("An LLM-judge scorer requires a judge model.", nameof(judgeModel));

        return new ScorerDescriptor(ScorerKind.LlmJudge, rubric.Trim(), judgeModel.Trim());
    }

    /// <summary>
    /// Returns an equivalent descriptor as a distinct instance. Each persisted <see cref="Score"/>
    /// owns its scorer identity as a value object, and EF Core owned types cannot share one CLR
    /// instance across multiple owners — so a run scoring many fixtures with the same scorer gives
    /// each <see cref="Score"/> its own copy.
    /// </summary>
    internal ScorerDescriptor Copy() => new(Kind, Config, JudgeModel);

    /// <summary>
    /// A stable, deterministic identity string for this scorer, used to group scores into a series
    /// across runs. Two descriptors differing in any identity component (kind, config, judge model)
    /// hash to different values; identical descriptors always hash to the same value.
    /// </summary>
    public string Identity
    {
        get
        {
            var canonical = $"{Kind}{Config}{JudgeModel ?? string.Empty}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
