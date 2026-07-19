using Api.Auth;
using Application.Identity;
using Application.Ports;
using Domain;

namespace Api.Admin;

/// <summary>
/// Admin organization management (spec 4.4): list all orgs with member counts, create/rename/delete
/// (delete cascades folders/prompts/datasets/runs — 1.10), and drill into an org's members
/// (list/add/remove). Every handler is gated by the 1.13 global-admin flag via
/// <see cref="OrgAccess.IsGlobalAdminAsync"/>. This is a *management* surface — the global-admin flag
/// grants it access to every org's management, not to any org's *content* (data endpoints stay
/// membership-gated). Owner-facing member management on the org's own page is spec 4.5.
/// </summary>
public static class AdminOrganizationEndpoints
{
    public static IEndpointRouteBuilder MapAdminOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/organizations").RequireAuthorization();

        group.MapGet("/", async (
            IOrganizationRepository organizations, IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var orgs = await organizations.ListAsync(ct);
            var counts = await users.CountMembersByOrganizationAsync(ct);
            return Results.Ok(orgs.Select(o =>
                OrganizationAdminResponse.From(o, counts.TryGetValue(o.Id, out var n) ? n : 0)));
        });

        group.MapPost("/", async (
            CreateOrgRequest request, IOrganizationRepository organizations, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            try
            {
                var org = Organization.Create(request.Name);
                await organizations.AddAsync(org, ct);
                return Results.Created($"/api/admin/organizations/{org.Id}",
                    OrganizationAdminResponse.From(org, 0));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (
            Guid id, RenameOrgRequest request, IOrganizationRepository organizations, OrgAccess access,
            IUserDirectory users, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            var org = await organizations.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();
            try
            {
                org.Rename(request.Name);
                await organizations.SaveChangesAsync(ct);
                var counts = await users.CountMembersByOrganizationAsync(ct);
                return Results.Ok(OrganizationAdminResponse.From(org, counts.TryGetValue(id, out var n) ? n : 0));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Cascade delete (1.10). Idempotent — a missing org is a no-op 204.
        group.MapDelete("/{id:guid}", async (
            Guid id, IOrganizationRepository organizations, IUserDirectory users, OrgAccess access,
            CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            await organizations.DeleteAsync(id, ct);
            // Revoke memberships too — they're in the Identity context and don't cascade (2.21).
            await users.RemoveAllMembersAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/members", async (
            Guid id, IOrganizationRepository organizations, IUserDirectory users, OrgAccess access,
            CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();
            var members = await users.ListOrganizationMembersAsync(id, ct);
            return Results.Ok(members.Select(OrgMemberResponse.From));
        });

        group.MapPost("/{id:guid}/members", async (
            Guid id, AddOrgMemberRequest request, IOrganizationRepository organizations, IUserDirectory users,
            OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            if (!Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();
            if (await users.FindByIdAsync(request.UserId, ct) is null)
                return Results.BadRequest(new { error = "User not found." });

            await users.GrantOrganizationAsync(request.UserId, id, role, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId, IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.IsGlobalAdminAsync(ct))
                return Results.Forbid();
            await users.RevokeOrganizationAsync(userId, id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
