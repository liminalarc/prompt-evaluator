using Application.Ports;
using Domain;

namespace Application.Prompts;

/// <summary>
/// Edits a version's editable metadata (its label — an optional description). Content and target
/// model stay immutable (run identity), so they are not part of the request. Returns null when the
/// prompt or the version does not exist so the Api can translate that to a 404.
/// </summary>
public sealed class EditPromptVersionHandler(IPromptRepository repository)
{
    public async Task<Prompt?> HandleAsync(
        Guid promptId, Guid versionId, string? label, CancellationToken ct = default)
    {
        var prompt = await repository.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        if (!prompt.EditVersionLabel(versionId, label))
            return null;

        await repository.SaveChangesAsync(ct);
        return prompt;
    }
}
