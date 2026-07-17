using System.Security.Claims;
using System.Text;
using Application.Ports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

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

            // Registration signs the new user in immediately. New users are never global admins.
            await SignInAsync(http, result.UserId, req.Email, req.DisplayName);
            return Results.Ok(new UserResponse(result.UserId, req.Email, req.DisplayName, IsAdmin: false));
        });

        group.MapPost("/login", async (LoginRequest req, IUserDirectory users, HttpContext http, CancellationToken ct) =>
        {
            var userId = await users.ValidateCredentialsAsync(req.Email, req.Password, ct);
            if (userId is null)
                return Results.Unauthorized();

            var account = await users.FindByEmailAsync(req.Email, ct);
            await SignInAsync(http, userId.Value, account!.Email, account.DisplayName);
            var isAdmin = await users.IsGlobalAdminAsync(userId.Value, ct);
            return Results.Ok(new UserResponse(userId.Value, account.Email, account.DisplayName, isAdmin));
        });

        // Always returns 200 with no hint whether the email exists (enumeration resistance). When it
        // does, a reset link carrying a base64url-encoded token is emailed.
        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest req, IUserDirectory users, IEmailSender email, IConfiguration config, CancellationToken ct) =>
        {
            var token = await users.GeneratePasswordResetTokenAsync(req.Email, ct);
            if (token is not null)
            {
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var webBaseUrl = config["Auth:WebBaseUrl"] ?? "http://localhost:4200";
                var link = $"{webBaseUrl}/reset-password?email={Uri.EscapeDataString(req.Email)}&token={encoded}";
                await email.SendAsync(new EmailMessage(
                    req.Email,
                    "Reset your LitmusAI password",
                    $"Reset your password using this link:\n{link}\n\nIf you didn't request this, ignore this email."), ct);
            }

            return Results.Ok(new { message = "If that account exists, a reset link has been sent." });
        });

        group.MapPost("/reset-password", async (ResetPasswordRequest req, IUserDirectory users, CancellationToken ct) =>
        {
            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { errors = new[] { "Invalid or expired reset token." } });
            }

            var result = await users.ResetPasswordAsync(req.Email, token, req.NewPassword, ct);
            return result.Succeeded ? Results.Ok() : Results.BadRequest(new { errors = result.Errors });
        });

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(Scheme);
            return Results.NoContent();
        }).RequireAuthorization();

        // Self-service change-password (4.3): a signed-in user rotates their own password by proving
        // the current one. No email; a wrong current password is a 400.
        group.MapPost("/change-password", async (
            ChangePasswordRequest req, ICurrentUser current, IUserDirectory users, CancellationToken ct) =>
        {
            var result = await users.ChangePasswordAsync(
                current.UserId!.Value, req.CurrentPassword, req.NewPassword, ct);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { errors = result.Errors });
        }).RequireAuthorization();

        group.MapGet("/me", async (ICurrentUser current, IUserDirectory users, HttpContext http, CancellationToken ct) =>
            Results.Ok(new UserResponse(
                current.UserId!.Value,
                http.User.FindFirstValue(ClaimTypes.Email) ?? "",
                http.User.FindFirstValue(DisplayNameClaim) ?? "",
                await users.IsGlobalAdminAsync(current.UserId!.Value, ct))))
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

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserResponse(Guid Id, string Email, string DisplayName, bool IsAdmin);
