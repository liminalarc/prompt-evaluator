using Domain;

namespace Api.Organizations;

public sealed record CreateOrganizationRequest(string Name);

public sealed record RenameOrganizationRequest(string Name);

public sealed record OrganizationResponse(Guid Id, string Name)
{
    public static OrganizationResponse From(Organization o) => new(o.Id, o.Name);
}
