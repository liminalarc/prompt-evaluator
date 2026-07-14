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

    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Grant (or upgrade) a user's access to an organization. No-op if already granted at that role.</summary>
    Task GrantOrganizationAsync(Guid userId, Guid organizationId, OrgRole role, CancellationToken ct = default);

    /// <summary>The organizations a user may access — the switcher and authZ read this.</summary>
    Task<IReadOnlyList<Guid>> GetAccessibleOrganizationIdsAsync(Guid userId, CancellationToken ct = default);

    Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}

/// <summary>A user projected free of any identity-framework types.</summary>
public sealed record UserAccount(Guid Id, string Email, string DisplayName);

public sealed record UserRegistrationResult(bool Succeeded, Guid UserId, IReadOnlyList<string> Errors)
{
    public static UserRegistrationResult Success(Guid userId) => new(true, userId, []);
    public static UserRegistrationResult Failure(IReadOnlyList<string> errors) => new(false, Guid.Empty, errors);
}
