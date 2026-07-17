using Application.Ports;
using Domain;

namespace Api.Admin;

/// <summary>An organization with its member count — the admin Organizations list row (4.4).</summary>
public sealed record OrganizationAdminResponse(Guid Id, string Name, int MemberCount)
{
    public static OrganizationAdminResponse From(Organization o, int memberCount) => new(o.Id, o.Name, memberCount);
}

/// <summary>One member of an org in the admin drill-in view (4.4).</summary>
public sealed record OrgMemberResponse(Guid UserId, string Email, string DisplayName, string Role)
{
    public static OrgMemberResponse From(OrgMemberInfo m) => new(m.UserId, m.Email, m.DisplayName, m.Role.ToString());
}

public sealed record CreateOrgRequest(string Name);

public sealed record RenameOrgRequest(string Name);

public sealed record AddOrgMemberRequest(Guid UserId, string Role);
