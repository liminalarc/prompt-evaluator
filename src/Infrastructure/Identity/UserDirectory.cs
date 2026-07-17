using Application.Identity;
using Application.Ports;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// The <see cref="IUserDirectory"/> adapter over ASP.NET Core Identity. <see cref="UserManager{T}"/>
/// owns credential hashing (PBKDF2) and user lookup; the identity <see cref="AppIdentityDbContext"/>
/// owns the per-organization access grants. All framework identity types stay behind this class.
/// </summary>
public sealed class UserDirectory(UserManager<AppUser> users, AppIdentityDbContext db) : IUserDirectory
{
    public async Task<UserRegistrationResult> RegisterAsync(
        string email, string displayName, string password, CancellationToken ct = default)
    {
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = displayName };
        var result = await users.CreateAsync(user, password);
        return result.Succeeded
            ? UserRegistrationResult.Success(user.Id)
            : UserRegistrationResult.Failure(result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<Guid?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return null;
        return await users.CheckPasswordAsync(user, password) ? user.Id : null;
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(email);
        return user is null ? null : await users.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            // Same generic failure as a bad token — never reveal whether the email exists.
            return PasswordResetResult.Failure(["Invalid or expired reset token."]);

        var result = await users.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? PasswordResetResult.Success()
            : PasswordResetResult.Failure(result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Project(await users.FindByEmailAsync(email));

    public async Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken ct = default)
        => Project(await users.FindByIdAsync(userId.ToString()));

    public async Task GrantOrganizationAsync(
        Guid userId, Guid organizationId, OrgRole role, CancellationToken ct = default)
    {
        var existing = await db.OrganizationMemberships
            .SingleOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, ct);

        if (existing is null)
        {
            db.OrganizationMemberships.Add(new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Role = role,
            });
        }
        else
        {
            existing.Role = role;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleOrganizationIdsAsync(Guid userId, CancellationToken ct = default)
        => await db.OrganizationMemberships
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync(ct);

    public Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
        => db.OrganizationMemberships.AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId, ct);

    public async Task<bool> IsGlobalAdminAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user?.IsAdmin ?? false;
    }

    public async Task SetGlobalAdminAsync(Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.IsAdmin == isAdmin)
            return;
        user.IsAdmin = isAdmin;
        await users.UpdateAsync(user);
    }

    public async Task<IReadOnlyList<UserAccountDetail>> ListUsersAsync(CancellationToken ct = default)
    {
        var all = await users.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var byUser = (await db.OrganizationMemberships.ToListAsync(ct))
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return all.Select(u => new UserAccountDetail(
            u.Id,
            u.Email ?? "",
            u.DisplayName,
            u.IsAdmin,
            (byUser.TryGetValue(u.Id, out var ms) ? ms : [])
                .Select(m => new OrgMembershipInfo(m.OrganizationId, m.Role))
                .ToList())).ToList();
    }

    public async Task RevokeOrganizationAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var existing = await db.OrganizationMemberships
            .SingleOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, ct);
        if (existing is null)
            return;
        db.OrganizationMemberships.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    public Task<int> CountGlobalAdminsAsync(CancellationToken ct = default)
        => users.Users.CountAsync(u => u.IsAdmin, ct);

    public async Task<PasswordResetResult> SetPasswordAsync(
        Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return PasswordResetResult.Failure(["User not found."]);

        // No email: generate and consume a reset token server-side so Identity's password policy
        // and security-stamp rotation still apply.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? PasswordResetResult.Success()
            : PasswordResetResult.Failure(result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<PasswordResetResult> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return PasswordResetResult.Failure(["User not found."]);

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded
            ? PasswordResetResult.Success()
            : PasswordResetResult.Failure(result.Errors.Select(e => e.Description).ToList());
    }

    private static UserAccount? Project(AppUser? user)
        => user is null ? null : new UserAccount(user.Id, user.Email ?? "", user.DisplayName);
}
