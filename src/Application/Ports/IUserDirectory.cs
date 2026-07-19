using Application.Identity;

namespace Application.Ports;

/// <summary>
/// The Identity bounded context's user store, exposed to Application as a port (4.1). The
/// Infrastructure adapter implements it over ASP.NET Core Identity (<c>UserManager</c> + an
/// identity <c>DbContext</c>), so no framework identity types leak past this seam. Also carries the
/// per-organization access grants that back authorization (the organization is the boundary, 1.9).
/// Cookie sign-in/out is an Api concern and deliberately absent here.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Create a new account (self-service registration). Idempotent-unfriendly: a duplicate email fails.</summary>
    Task<UserRegistrationResult> RegisterAsync(string email, string displayName, string password, CancellationToken ct = default);

    /// <summary>Returns the user id when the email+password are valid, otherwise null.</summary>
    Task<Guid?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// A single-use password reset token for the user with this email, or null if no such user.
    /// The caller decides how to deliver it (email) and must not reveal which case occurred.
    /// </summary>
    Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken ct = default);

    /// <summary>Consume a reset token and set a new password; also rotates the user's security stamp.</summary>
    Task<PasswordResetResult> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);

    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Grant (or upgrade) a user's access to an organization. No-op if already granted at that role.</summary>
    Task GrantOrganizationAsync(Guid userId, Guid organizationId, OrgRole role, CancellationToken ct = default);

    /// <summary>The organizations a user may access — the switcher and authZ read this.</summary>
    Task<IReadOnlyList<Guid>> GetAccessibleOrganizationIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// A user's memberships with the role held in each (4.5). Backs the caller's per-org role on the
    /// switcher payload and the owner-or-admin check for member management. Empty when a member of none.
    /// </summary>
    Task<IReadOnlyList<OrgMembershipInfo>> GetUserMembershipsAsync(Guid userId, CancellationToken ct = default);

    Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Whether the user is a workspace-level global admin (spec 1.13) — the gate for managing
    /// workspace-wide resources like the Model Catalog. Distinct from per-org membership/role.
    /// </summary>
    Task<bool> IsGlobalAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Set (or clear) a user's global-admin flag. Used by the bootstrap-admin seeder.</summary>
    Task SetGlobalAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>All users with their admin flag and org memberships — the admin user-management view (4.3).</summary>
    Task<IReadOnlyList<UserAccountDetail>> ListUsersAsync(CancellationToken ct = default);

    /// <summary>Remove a user's membership of an organization (4.3). No-op if not a member.</summary>
    Task RevokeOrganizationAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Revoke <em>every</em> membership of an organization — called when the org is deleted (2.21) so
    /// no dangling membership row survives it (memberships live in the Identity context with no
    /// cross-context FK, so an org delete can't cascade to them). No-op when the org has no members.
    /// </summary>
    Task RemoveAllMembersAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>How many users hold the global-admin flag — backs the last-admin lockout guard (4.3).</summary>
    Task<int> CountGlobalAdminsAsync(CancellationToken ct = default);

    /// <summary>Member count keyed by organization id — backs the admin Organizations list (4.4). Orgs with no members are absent.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountMembersByOrganizationAsync(CancellationToken ct = default);

    /// <summary>The members of one organization with their role — the admin org drill-in (4.4).</summary>
    Task<IReadOnlyList<OrgMemberInfo>> ListOrganizationMembersAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Admin-set a user's password directly, no email/token round-trip (4.3). Policy still applies.</summary>
    Task<PasswordResetResult> SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);

    /// <summary>Change a user's own password after verifying the current one (4.3, self-service).</summary>
    Task<PasswordResetResult> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);

    // ── Request-to-join access (2.21) ─────────────────────────────────────────────────────────────
    // The pull path: a user requests access to an existing org; an owner/admin approves (→ a grant)
    // or denies. Domain rules: can't request an org you're already in; no duplicate open request;
    // approve is idempotent (re-approving re-grants the same membership without error).

    /// <summary>Create a Pending access request. Rejects when the user is already a member or already has an open request.</summary>
    Task<AccessRequestCreateResult> RequestOrganizationAccessAsync(
        Guid requesterId, Guid organizationId, OrgRole requestedRole, CancellationToken ct = default);

    /// <summary>The still-Pending requests for one org — the owner's Requests queue (projected with requester email/name).</summary>
    Task<IReadOnlyList<AccessRequestInfo>> ListPendingAccessRequestsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>A requester's own access requests across all orgs — backs the requester-side state (e.g. "pending").</summary>
    Task<IReadOnlyList<AccessRequestInfo>> ListAccessRequestsByRequesterAsync(Guid requesterId, CancellationToken ct = default);

    /// <summary>Fetch one access request by id (projected), or null. Lets an endpoint check it belongs to the route's org.</summary>
    Task<AccessRequestInfo?> GetAccessRequestAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Approve a request: grant the requester membership (at <paramref name="role"/>, or the requested
    /// role when null) and mark it Approved — atomically, one SaveChanges. Idempotent.
    /// </summary>
    Task<AccessRequestDecisionResult> ApproveAccessRequestAsync(
        Guid requestId, Guid deciderId, OrgRole? role, CancellationToken ct = default);

    /// <summary>Deny a Pending request. A no-op-conflict when it was already approved (a grant can't be un-made here).</summary>
    Task<AccessRequestDecisionResult> DenyAccessRequestAsync(Guid requestId, Guid deciderId, CancellationToken ct = default);
}

/// <summary>One access request projected with the requester's email/name — the owner's Requests queue (2.21).</summary>
public sealed record AccessRequestInfo(
    Guid Id, Guid RequesterId, string RequesterEmail, string RequesterDisplayName,
    Guid OrganizationId, OrgRole RequestedRole, AccessRequestStatus Status, DateTimeOffset CreatedAt);

public enum AccessRequestCreateOutcome { Created, AlreadyMember, DuplicateOpenRequest }

/// <summary>The outcome of creating an access request; <see cref="RequestId"/> is set for Created and the existing-open case.</summary>
public sealed record AccessRequestCreateResult(AccessRequestCreateOutcome Outcome, Guid RequestId);

public enum AccessRequestDecisionOutcome { Approved, Denied, NotFound, AlreadyApproved }

/// <summary>The outcome of an approve/deny; <see cref="Request"/> carries the updated projection on success.</summary>
public sealed record AccessRequestDecisionResult(AccessRequestDecisionOutcome Outcome, AccessRequestInfo? Request);

/// <summary>A user projected with their admin flag and org memberships — the admin list view (4.3).</summary>
public sealed record UserAccountDetail(
    Guid Id, string Email, string DisplayName, bool IsAdmin, IReadOnlyList<OrgMembershipInfo> Memberships);

/// <summary>One org membership of a user: the org id and the role held there.</summary>
public sealed record OrgMembershipInfo(Guid OrganizationId, OrgRole Role);

/// <summary>One member of an organization, projected with their email/name and role — the admin org drill-in (4.4).</summary>
public sealed record OrgMemberInfo(Guid UserId, string Email, string DisplayName, OrgRole Role);

/// <summary>A user projected free of any identity-framework types.</summary>
public sealed record UserAccount(Guid Id, string Email, string DisplayName);

public sealed record UserRegistrationResult(bool Succeeded, Guid UserId, IReadOnlyList<string> Errors)
{
    public static UserRegistrationResult Success(Guid userId) => new(true, userId, []);
    public static UserRegistrationResult Failure(IReadOnlyList<string> errors) => new(false, Guid.Empty, errors);
}

public sealed record PasswordResetResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static PasswordResetResult Success() => new(true, []);
    public static PasswordResetResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}
