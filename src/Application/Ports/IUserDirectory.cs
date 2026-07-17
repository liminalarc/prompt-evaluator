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

    Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Whether the user is a workspace-level global admin (spec 1.13) — the gate for managing
    /// workspace-wide resources like the Model Catalog. Distinct from per-org membership/role.
    /// </summary>
    Task<bool> IsGlobalAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Set (or clear) a user's global-admin flag. Used by the bootstrap-admin seeder.</summary>
    Task SetGlobalAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default);
}

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
