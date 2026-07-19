using Application.Ports;

namespace Application.Prompts;

/// <summary>
/// Marks a version as <b>Current in source</b> (1.16) — the single mechanic behind the initial marker
/// and mark-as-backported. Stamps the set time from <see cref="TimeProvider"/> and records the optional
/// commit SHA. Distinguishes a missing prompt from a version that isn't in the prompt's history so the
/// Api can return the right status.
/// </summary>
public sealed class SetCurrentVersionHandler(IPromptRepository repository, TimeProvider time)
{
    public enum Outcome { Ok, PromptNotFound, VersionNotFound }

    public async Task<Outcome> HandleAsync(
        Guid promptId, Guid versionId, string? commitSha, CancellationToken ct = default)
    {
        var prompt = await repository.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return Outcome.PromptNotFound;

        if (!prompt.SetCurrentVersion(versionId, commitSha, time.GetUtcNow()))
            return Outcome.VersionNotFound;

        await repository.SaveChangesAsync(ct);
        return Outcome.Ok;
    }
}
