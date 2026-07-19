namespace Application.Analytics;

/// <summary>
/// One prompt version's lifecycle status (1.16): whether it's the version the source app runs
/// (<see cref="IsCurrent"/>), whether it scores higher than Current on a shared dataset and so should
/// be shipped (<see cref="BackportEligible"/>), and whether it regressed vs. its prior version
/// (<see cref="Regressed"/>). The flags are independent — the UI renders whichever apply as badges.
/// </summary>
public sealed record VersionStatus(
    Guid VersionId,
    int VersionNumber,
    string? Label,
    bool IsCurrent,
    bool BackportEligible,
    bool Regressed);

/// <summary>
/// A prompt's per-version status set (1.16) plus its Current-in-source pointer (null until first set).
/// </summary>
public sealed record PromptVersionStatus(
    Guid PromptId,
    Guid? CurrentVersionId,
    IReadOnlyList<VersionStatus> Versions);
