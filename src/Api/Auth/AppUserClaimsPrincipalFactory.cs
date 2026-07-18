using System.Security.Claims;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Api.Auth;

/// <summary>
/// Builds the cookie principal when signing a user in through <see cref="SignInManager{TUser}"/> (3.2).
/// The base factory already stamps the user id (NameIdentifier) and the security stamp claim the
/// <c>SecurityStampValidator</c> checks; this adds the email + display-name claims the API reads back
/// (e.g. <c>/api/auth/me</c>).
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser>(userManager, options)
{
    public override async Task<ClaimsPrincipal> CreateAsync(AppUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        if (!string.IsNullOrEmpty(user.Email))
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(AuthEndpoints.DisplayNameClaim, user.DisplayName));

        return principal;
    }
}
