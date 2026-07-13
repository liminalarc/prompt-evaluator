using Application.Ports;
using Domain;

namespace Application.Folders;

/// <summary>
/// Files a prompt into a folder, or unfiles it (moves it back to the root) when the target folder
/// is null. Returns null when the prompt does not exist (Api → 404); throws
/// <see cref="ArgumentException"/> (Api → 400) when the named target folder does not exist.
/// </summary>
public sealed class MovePromptHandler(IPromptRepository prompts, IFolderRepository folders)
{
    public async Task<Prompt?> HandleAsync(Guid promptId, Guid? folderId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        if (prompt is null)
            return null;

        if (folderId is Guid fid)
        {
            var folder = await folders.GetByIdAsync(fid, ct);
            if (folder is null)
                throw new ArgumentException("Target folder does not exist.", nameof(folderId));
            prompt.MoveToFolder(fid);
        }
        else
        {
            prompt.Unfile();
        }

        await prompts.SaveChangesAsync(ct);
        return prompt;
    }
}
