using Api.Auth;
using Application.Identity;
using Application.Ports;
using Domain;

namespace Api.Organizations;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations").RequireAuthorization();

        // The switcher: only the organizations the current user is a member of (4.1), each stamped
        // with the caller's role in it (4.5) so the client can gate owner-only UI.
        group.MapGet("/", async (IOrganizationRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var roles = await access.MyOrgRolesAsync(ct);
            var orgs = await repository.ListAsync(ct);
            return Results.Ok(orgs
                .Where(o => roles.ContainsKey(o.Id))
                .Select(o => OrganizationResponse.From(o, roles[o.Id])));
        });

        group.MapPost("/", async (CreateOrganizationRequest request, IOrganizationRepository repository,
            ICurrentUser current, IUserDirectory users, CancellationToken ct) =>
        {
            try
            {
                var org = Organization.Create(request.Name);
                await repository.AddAsync(org, ct);

                // Grant the creator ownership — otherwise they couldn't access what they just made (4.1).
                if (current.UserId is { } uid)
                    await users.GrantOrganizationAsync(uid, org.Id, OrgRole.Owner, ct);

                return Results.Created($"/api/organizations/{org.Id}",
                    OrganizationResponse.From(org, OrgRole.Owner));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameOrganizationRequest request,
            IOrganizationRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var org = await repository.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();
            if (!await access.CanAccessOrgAsync(id, ct))
                return Results.Forbid();
            try
            {
                org.Rename(request.Name);
                await repository.SaveChangesAsync(ct);
                var role = (await access.MyOrgRolesAsync(ct)).TryGetValue(id, out var r) ? r : OrgRole.Member;
                return Results.Ok(OrganizationResponse.From(org, role));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Deletes an org and everything under it (folders, prompts, datasets cascade). Idempotent —
        // a missing org is a no-op 204; a member is required to delete one that exists (4.1).
        group.MapDelete("/{id:guid}", async (Guid id, IOrganizationRepository repository,
            IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            var org = await repository.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NoContent();
            if (!await access.CanAccessOrgAsync(id, ct))
                return Results.Forbid();

            await repository.DeleteAsync(id, ct);
            // Memberships live in the Identity context (no cross-context FK cascade) — revoke them
            // explicitly so the deleted org leaves no dangling membership rows (2.21).
            await users.RemoveAllMembersAsync(id, ct);
            return Results.NoContent();
        });

        MapMemberEndpoints(group);
        return app;
    }

    /// <summary>
    /// Owner-facing member management (4.5): list/add-by-email/set-role/remove, gated
    /// owner-or-admin per org (<see cref="OrgAccess.CanManageOrgMembersAsync"/>) → 403 otherwise.
    /// A last-owner guard blocks demoting/removing an org's final Owner here; a global admin can
    /// still fix an org via the 4.4 admin surface (no guard there).
    /// </summary>
    private static void MapMemberEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/members", async (
            Guid id, IOrganizationRepository organizations, IUserDirectory users, OrgAccess access,
            CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();
            var members = await users.ListOrganizationMembersAsync(id, ct);
            return Results.Ok(members.Select(OrgMemberResponse.From));
        });

        group.MapPost("/{id:guid}/members", async (
            Guid id, AddOrgMemberByEmailRequest request, IOrganizationRepository organizations,
            IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (!Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();

            var user = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : await users.FindByEmailAsync(request.Email.Trim(), ct);
            if (user is null)
                return Results.BadRequest(new { error = "No user with that email. Users must register first." });

            await users.GrantOrganizationAsync(user.Id, id, role, ct);
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId, SetOrgMemberRoleRequest request, IOrganizationRepository organizations,
            IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (!Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();

            var members = await users.ListOrganizationMembersAsync(id, ct);
            var target = members.FirstOrDefault(m => m.UserId == userId);
            if (target is null)
                return Results.NotFound();

            // Last-owner guard: don't let the final Owner be demoted out of ownership.
            if (target.Role == OrgRole.Owner && role != OrgRole.Owner && IsLastOwner(members, userId))
                return Results.BadRequest(new { error = "An organization must keep at least one owner." });

            await users.GrantOrganizationAsync(userId, id, role, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId, IOrganizationRepository organizations, IUserDirectory users,
            OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();

            var members = await users.ListOrganizationMembersAsync(id, ct);
            var target = members.FirstOrDefault(m => m.UserId == userId);
            if (target is null)
                return Results.NoContent(); // already not a member — idempotent

            // Last-owner guard: don't strand the org with no owner.
            if (target.Role == OrgRole.Owner && IsLastOwner(members, userId))
                return Results.BadRequest(new { error = "An organization must keep at least one owner." });

            await users.RevokeOrganizationAsync(userId, id, ct);
            return Results.NoContent();
        });
    }

    private static bool IsLastOwner(IReadOnlyList<OrgMemberInfo> members, Guid userId)
        => members.Count(m => m.Role == OrgRole.Owner) == 1
           && members.Single(m => m.Role == OrgRole.Owner).UserId == userId;
}
