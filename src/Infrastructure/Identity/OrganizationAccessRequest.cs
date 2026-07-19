using Application.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// A user's request to join an organization (2.21) — the pull counterpart to the 4.5 add-by-email
/// push. Lives in the Identity context alongside <see cref="OrganizationMembership"/> because an
/// approval grants a membership; ids are plain <see cref="Guid"/>s (no cross-context FK), matching
/// the rest of the app. Domain rules (no duplicate open request, can't request an org you're in,
/// idempotent approve) are enforced by the <c>UserDirectory</c> adapter + a partial unique index.
/// </summary>
public sealed class OrganizationAccessRequest
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid OrganizationId { get; set; }
    public OrgRole RequestedRole { get; set; }
    public AccessRequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
}
