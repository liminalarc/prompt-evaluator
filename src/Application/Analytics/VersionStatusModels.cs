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
///
/// <see cref="CrossModelVersionsExcluded"/> (R9, 2.9a) is how many versions were left out of the backport
/// comparison because they ran on a <b>different subject model</b> than Current — eligibility and the
/// target hold the subject model constant, so a version scored on a stronger model can't win on the model
/// rather than the prompt. 0 when no Current is set. The UI surfaces a warning when it is positive.
/// </summary>
public sealed record PromptVersionStatus(
    Guid PromptId,
    Guid? CurrentVersionId,
    Guid? BackportTargetVersionId,
    IReadOnlyList<VersionStatus> Versions,
    int CrossModelVersionsExcluded);
