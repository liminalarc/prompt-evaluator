using Application.Ports;
using Domain;

namespace Application.Prompts;

/// <summary>
/// Appends an immutable version to an existing prompt, stamping it with the current time.
/// Returns null when the prompt does not exist so the Api can translate that to a 404.
/// </summary>
public sealed class AddPromptVersionHandler(IPromptRepository repository, TimeProvider time)
{
    public async Task<Prompt?> HandleAsync(
        Guid promptId,
        string content,
        string targetModel,
        string? label,
        string? sourceApp,
        CancellationToken ct = default)
    {
        var prompt = await repository.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        prompt.AddVersion(content, targetModel, time.GetUtcNow(), label, sourceApp);
        await repository.SaveChangesAsync(ct);
        return prompt;
    }
}
