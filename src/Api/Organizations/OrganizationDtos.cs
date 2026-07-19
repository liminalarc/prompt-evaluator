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

// ── Request-to-join access (2.21) ─────────────────────────────────────────────────────────────────

/// <summary>Request to join an org; role is optional (defaults to Member) and is only a request.</summary>
public sealed record CreateAccessRequestRequest(string? Role);

/// <summary>Approve a pending request; role is optional and overrides the requested role when set.</summary>
public sealed record ApproveAccessRequestRequest(string? Role);

/// <summary>One access request in the owner's Requests queue (2.21), projected with the requester's identity.</summary>
public sealed record AccessRequestResponse(
    Guid Id, Guid RequesterId, string RequesterEmail, string RequesterDisplayName,
    Guid OrganizationId, string RequestedRole, string Status, DateTimeOffset CreatedAt)
{
    public static AccessRequestResponse From(AccessRequestInfo r) =>
        new(r.Id, r.RequesterId, r.RequesterEmail, r.RequesterDisplayName, r.OrganizationId,
            r.RequestedRole.ToString(), r.Status.ToString(), r.CreatedAt);
}

/// <summary>
/// One org in the discovery directory (2.21): every org, with the current user's relationship to it
/// so the client can show "Member" / "Requested" / a "Request access" action. Names are visible
/// workspace-wide — the accepted privacy trade-off of the directory discovery model.
/// </summary>
public sealed record OrgDirectoryEntryResponse(Guid Id, string Name, bool IsMember, bool HasPendingRequest);
