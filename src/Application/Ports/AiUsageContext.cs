namespace Application.Ports;

/// <summary>
/// Who a model call is attributable to (6.1) — the organization and user on whose behalf the harness
/// called the eval-runner. The <em>feature</em> (subject/judge/generate) is intrinsic to the call site,
/// so only org + user are carried here.
/// </summary>
public readonly record struct AiUsageAttribution(Guid? OrganizationId, Guid? UserId);

/// <summary>
/// Ambient carrier for the current <see cref="AiUsageAttribution"/>. Application handlers
/// <see cref="Begin"/> a scope around an eval operation; the eval-runner transport reads
/// <see cref="Current"/> when recording usage. Ambient (not an explicit param) because the judge call
/// runs indirectly through <c>LlmJudgeScorer</c> / <c>IScorer</c>, which carry no attribution.
/// </summary>
public interface IAiUsageContextAccessor
{
    /// <summary>The attribution in effect for the current async flow; default (nulls) when none is set.</summary>
    AiUsageAttribution Current { get; }

    /// <summary>Sets the ambient attribution until the returned scope is disposed (restores the prior value).</summary>
    IDisposable Begin(AiUsageAttribution attribution);
}
