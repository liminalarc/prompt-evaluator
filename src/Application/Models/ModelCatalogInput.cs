using Domain;

namespace Application.Models;

/// <summary>
/// Parses the string-shaped provider/role inputs the Api receives into the domain enums, throwing
/// <see cref="ArgumentException"/> (→ 400 at the edge) on anything unrecognized. Accepts the
/// lowercase role names the SPA sends (<c>subject</c>/<c>judge</c>/<c>generator</c>).
/// </summary>
public static class ModelCatalogInput
{
    public static ModelProvider ParseProvider(string provider)
    {
        if (Enum.TryParse<ModelProvider>(provider, ignoreCase: true, out var parsed))
            return parsed;
        throw new ArgumentException($"Unknown provider '{provider}'.", nameof(provider));
    }

    public static IReadOnlyCollection<ModelRole> ParseRoles(IEnumerable<string>? roles)
    {
        if (roles is null)
            throw new ArgumentException("A model must serve at least one role.", nameof(roles));

        var parsed = new List<ModelRole>();
        foreach (var role in roles)
        {
            if (!Enum.TryParse<ModelRole>(role, ignoreCase: true, out var value))
                throw new ArgumentException($"Unknown role '{role}'.", nameof(roles));
            parsed.Add(value);
        }
        return parsed;
    }
}
