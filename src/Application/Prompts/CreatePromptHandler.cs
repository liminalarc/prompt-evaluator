using Application.Ports;
using Domain;

namespace Application.Prompts;

/// <summary>
/// Registers a new prompt (copy-in) under an organization (1.9). Versions are added separately.
/// Returns null when the organization does not exist (Api → 404).
/// </summary>
public sealed class CreatePromptHandler(IOrganizationRepository organizations, IPromptRepository repository)
{
    public async Task<Prompt?> HandleAsync(
        Guid organizationId, string name, string? description, CancellationToken ct = default)
    {
        var org = await organizations.GetByIdAsync(organizationId, ct);
        if (org is null)
            return null;

        var prompt = Prompt.Create(organizationId, name, description);
        await repository.AddAsync(prompt, ct);
        return prompt;
    }
}
