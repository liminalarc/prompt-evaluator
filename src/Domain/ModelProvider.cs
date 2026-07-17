namespace Domain;

/// <summary>
/// The LLM provider a catalog model routes to. Mirrors the eval-runner's provider registry
/// (spec 1.5) — the eval-runner remains the authority on which providers are actually configured
/// (spec 1.13); this enum is the .NET-side catalog's grouping, stored as a string.
/// </summary>
public enum ModelProvider
{
    Anthropic,
    OpenAi,
}
