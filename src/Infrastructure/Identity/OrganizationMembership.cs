using Application.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// A grant of a user's access to an organization (4.1) — the join that backs authorization. The
/// organization is the permission boundary (1.9); membership here is resolved against
/// <c>Prompt.OrganizationId</c> to allow or forbid access. <see cref="OrganizationId"/> is a plain
/// <see cref="Guid"/> (no cross-context FK) — the same shape the rest of the app uses for org ids.
/// </summary>
public sealed class OrganizationMembership
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public OrgRole Role { get; set; }
}
