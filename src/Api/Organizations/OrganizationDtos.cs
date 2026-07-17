using Application.Identity;
using Application.Ports;
using Domain;

namespace Api.Organizations;

public sealed record CreateOrganizationRequest(string Name);

public sealed record RenameOrganizationRequest(string Name);

/// <summary>
/// An org in the member-scoped switcher list (1.9). Carries the caller's <see cref="Role"/> in that
/// org (4.5) so the client can gate owner-only UI (the server still enforces authoritatively).
/// </summary>
public sealed record OrganizationResponse(Guid Id, string Name, string Role)
{
    public static OrganizationResponse From(Organization o, OrgRole role) => new(o.Id, o.Name, role.ToString());
}

/// <summary>One member of an org — the owner-facing member view (4.5).</summary>
public sealed record OrgMemberResponse(Guid UserId, string Email, string DisplayName, string Role)
{
    public static OrgMemberResponse From(OrgMemberInfo m) => new(m.UserId, m.Email, m.DisplayName, m.Role.ToString());
}

/// <summary>Add a member to an org by email (4.5) — an owner can't enumerate the user directory.</summary>
public sealed record AddOrgMemberByEmailRequest(string Email, string Role);

/// <summary>Set an existing member's role (4.5).</summary>
public sealed record SetOrgMemberRoleRequest(string Role);
