using Api.Auth;
using Application.Identity;
using Application.Ports;

namespace Api.Admin;

/// <summary>
/// Admin user & access management (spec 4.3): list users, toggle the workspace global-admin flag,
/// grant/revoke org membership + role, and set a password — all gated by the [[1.13]] global-admin
/// flag via <see cref="OrgAccess.IsGlobalAdminAsync"/>. No email; account creation stays self-service.
/// </summary>
public static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users").RequireAuthorization();

        group.MapGet("/", async (IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var list = await users.ListUsersAsync(ct);
            return Results.Ok(list.Select(UserDetailResponse.From));
        });

        // Admin-created users (4.6): create an account directly (no email, no self-registration). The
        // new user is a plain member of nothing — org/role + admin are granted afterward with the
        // per-user controls below. Reuses the same RegisterAsync the self-serve /register path uses.
        group.MapPost("/", async (
            CreateUserRequest request, IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();

            var result = await users.RegisterAsync(request.Email, request.DisplayName, request.Password, ct);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors });

            var created = new UserDetailResponse(
                result.UserId, request.Email, request.DisplayName, IsAdmin: false, []);
            return Results.Created($"/api/admin/users/{result.UserId}", created);
        });

        group.MapPost("/{id:guid}/admin", async (
            Guid id, SetAdminRequest request, IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();

            // Lockout guard: never remove the last global admin.
            if (!request.IsAdmin
                && await users.IsGlobalAdminAsync(id, ct)
                && await users.CountGlobalAdminsAsync(ct) <= 1)
            {
                return Results.BadRequest(new { error = "Cannot remove the last global admin." });
            }

            await users.SetGlobalAdminAsync(id, request.IsAdmin, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/organizations", async (
            Guid id, GrantMembershipRequest request, IUserDirectory users,
            IOrganizationRepository organizations, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            if (!Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
                return Results.BadRequest(new { error = "Organization not found." });

            await users.GrantOrganizationAsync(id, request.OrganizationId, role, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}/organizations/{orgId:guid}", async (
            Guid id, Guid orgId, IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            await users.RevokeOrganizationAsync(id, orgId, ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/password", async (
            Guid id, SetPasswordRequest request, IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var result = await users.SetPasswordAsync(id, request.NewPassword, ct);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { errors = result.Errors });
        });

        return app;
    }
}
