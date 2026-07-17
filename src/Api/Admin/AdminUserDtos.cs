using Application.Ports;

namespace Api.Admin;

public sealed record MembershipResponse(Guid OrganizationId, string Role)
{
    public static MembershipResponse From(OrgMembershipInfo m) =>
        new(m.OrganizationId, m.Role.ToString());
}

/// <summary>A user as seen on the admin user-management page (4.3).</summary>
public sealed record UserDetailResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsAdmin,
    IReadOnlyList<MembershipResponse> Memberships)
{
    public static UserDetailResponse From(UserAccountDetail u) =>
        new(u.Id, u.Email, u.DisplayName, u.IsAdmin, u.Memberships.Select(MembershipResponse.From).ToList());
}

public sealed record SetAdminRequest(bool IsAdmin);

public sealed record GrantMembershipRequest(Guid OrganizationId, string Role);

public sealed record SetPasswordRequest(string NewPassword);
