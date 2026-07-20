namespace Application.Analytics;

/// <summary>
/// One prompt version's lifecycle status (1.16): whether it's the version the source app runs
/// (<see cref="IsCurrent"/>), whether it scores higher than Current on a shared dataset
/// (<see cref="BackportEligible"/>) and — of those — whether it is <b>the single recommended backport
/// target</b> (<see cref="IsBackportTarget"/>, the highest-scoring version above Current), and whether
/// it regressed vs. its prior version (<see cref="Regressed"/>). The UI badges Current, the one
/// Backport target, and Regressed. <see cref="BackportEligible"/> stays in the model as the underlying
/// signal (all versions that beat Current) even though only the single target is surfaced as a badge.
/// </summary>
public sealed record VersionStatus(
    Guid VersionId,
    int VersionNumber,
    string? Label,
    bool IsCurrent,
    bool BackportEligible,
    bool IsBackportTarget,
    bool Regressed);

/// <summary>
/// A prompt's per-version status set (1.16) plus its Current-in-source pointer (null until first set)
/// and the single recommended <see cref="BackportTargetVersionId"/> (the highest-scoring version above
/// Current, or null when none beats it).
/// </summary>
public sealed record PromptVersionStatus(
    Guid PromptId,
    Guid? CurrentVersionId,
    Guid? BackportTargetVersionId,
    IReadOnlyList<VersionStatus> Versions);
