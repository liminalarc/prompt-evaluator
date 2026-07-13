using Application.Ports;
using Domain;

namespace Api.Organizations;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations");

        // Every organization one can access (access-filtered by 4.1 later; all for now) — the switcher.
        group.MapGet("/", async (IOrganizationRepository repository, CancellationToken ct) =>
        {
            var orgs = await repository.ListAsync(ct);
            return Results.Ok(orgs.Select(OrganizationResponse.From));
        });

        group.MapPost("/", async (CreateOrganizationRequest request, IOrganizationRepository repository, CancellationToken ct) =>
        {
            try
            {
                var org = Organization.Create(request.Name);
                await repository.AddAsync(org, ct);
                return Results.Created($"/api/organizations/{org.Id}", OrganizationResponse.From(org));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameOrganizationRequest request, IOrganizationRepository repository, CancellationToken ct) =>
        {
            var org = await repository.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();
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

        return app;
    }
}
