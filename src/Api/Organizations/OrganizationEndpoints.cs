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

        // The switcher: only the organizations the current user is a member of (4.1).
        group.MapGet("/", async (IOrganizationRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var accessible = await access.AccessibleOrgIdsAsync(ct);
            var orgs = await repository.ListAsync(ct);
            return Results.Ok(orgs.Where(o => accessible.Contains(o.Id)).Select(OrganizationResponse.From));
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

                return Results.Created($"/api/organizations/{org.Id}", OrganizationResponse.From(org));
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
                return Results.Ok(OrganizationResponse.From(org));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Deletes an org and everything under it (folders, prompts, datasets cascade). Idempotent —
        // a missing org is a no-op 204; a member is required to delete one that exists (4.1).
        group.MapDelete("/{id:guid}", async (Guid id, IOrganizationRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var org = await repository.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NoContent();
            if (!await access.CanAccessOrgAsync(id, ct))
                return Results.Forbid();

            await repository.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
