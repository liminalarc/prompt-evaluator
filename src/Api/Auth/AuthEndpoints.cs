using System.Security.Claims;
using Application.Ports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Api.Auth;

/// <summary>
/// First-party authentication endpoints (4.1): self-service registration, login, logout, and the
/// session-restore probe <c>/me</c>. Credential validation goes through the <see cref="IUserDirectory"/>
/// port; issuing/clearing the cookie is done here (an Api concern) via <c>HttpContext.SignInAsync</c>.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>The cookie authentication scheme the whole Api uses.</summary>
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (RegisterRequest req, IUserDirectory users, HttpContext http, CancellationToken ct) =>
        {
            var result = await users.RegisterAsync(req.Email, req.DisplayName, req.Password, ct);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors });

            // Registration signs the new user in immediately.
            await SignInAsync(http, result.UserId, req.Email, req.DisplayName);
            return Results.Ok(new UserResponse(result.UserId, req.Email, req.DisplayName));
        });

        group.MapPost("/login", async (LoginRequest req, IUserDirectory users, HttpContext http, CancellationToken ct) =>
        {
            var userId = await users.ValidateCredentialsAsync(req.Email, req.Password, ct);
            if (userId is null)
                return Results.Unauthorized();

            var account = await users.FindByEmailAsync(req.Email, ct);
            await SignInAsync(http, userId.Value, account!.Email, account.DisplayName);
            return Results.Ok(new UserResponse(userId.Value, account.Email, account.DisplayName));
        });

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(Scheme);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", (ICurrentUser current, HttpContext http) =>
            Results.Ok(new UserResponse(
                current.UserId!.Value,
                http.User.FindFirstValue(ClaimTypes.Email) ?? "",
                http.User.FindFirstValue(DisplayNameClaim) ?? "")))
            .RequireAuthorization();

        return app;
    }

    private const string DisplayNameClaim = "display_name";

    private static Task SignInAsync(HttpContext http, Guid userId, string email, string displayName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(DisplayNameClaim, displayName),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return http.SignInAsync(Scheme, principal);
    }
}

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(Guid Id, string Email, string DisplayName);
