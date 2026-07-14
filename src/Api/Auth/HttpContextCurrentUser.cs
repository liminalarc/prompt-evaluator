using System.Security.Claims;
using Application.Ports;

namespace Api.Auth;

/// <summary>
/// Resolves <see cref="ICurrentUser"/> from the current request's authenticated principal (4.1).
/// The user id is the <see cref="ClaimTypes.NameIdentifier"/> claim stamped into the auth cookie at
/// sign-in.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
