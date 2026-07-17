using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// The identity user for the Identity bounded context (4.1). Inherits the ASP.NET Core Identity
/// user (hashed credentials, security stamp, email) and adds a display name. Lives in Infrastructure
/// because it carries framework types; Application only ever sees it projected as
/// <see cref="Application.Ports.UserAccount"/> through the <see cref="Application.Ports.IUserDirectory"/> port.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Workspace-level global admin (spec 1.13) — distinct from the per-organization
    /// <see cref="Application.Identity.OrgRole"/>. Gates management of workspace-wide resources like
    /// the Model Catalog. The bootstrap admin is seeded with this set.
    /// </summary>
    public bool IsAdmin { get; set; }
}
