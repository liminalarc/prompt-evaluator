using Application.Ports;
using Domain;

namespace Application.Prompts;

/// <summary>Registers a new prompt (copy-in). Versions are added separately.</summary>
public sealed class CreatePromptHandler(IPromptRepository repository)
{
    public async Task<Prompt> HandleAsync(string name, string? description, CancellationToken ct = default)
    {
        var prompt = Prompt.Create(name, description);
        await repository.AddAsync(prompt, ct);
        return prompt;
    }
}
