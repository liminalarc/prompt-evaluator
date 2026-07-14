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
}
