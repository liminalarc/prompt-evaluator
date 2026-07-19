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

    public async Task<IReadOnlyList<OrgMembershipInfo>> GetUserMembershipsAsync(
        Guid userId, CancellationToken ct = default)
        => await db.OrganizationMemberships
            .Where(m => m.UserId == userId)
            .Select(m => new OrgMembershipInfo(m.OrganizationId, m.Role))
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

    public async Task RemoveAllMembersAsync(Guid organizationId, CancellationToken ct = default)
        => await db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId)
            .ExecuteDeleteAsync(ct);

    public Task<int> CountGlobalAdminsAsync(CancellationToken ct = default)
        => users.Users.CountAsync(u => u.IsAdmin, ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountMembersByOrganizationAsync(CancellationToken ct = default)
        => await db.OrganizationMemberships
            .GroupBy(m => m.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count, ct);

    public async Task<IReadOnlyList<OrgMemberInfo>> ListOrganizationMembersAsync(
        Guid organizationId, CancellationToken ct = default)
    {
        var memberships = await db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId)
            .ToListAsync(ct);
        var userIds = memberships.Select(m => m.UserId).ToList();
        var usersById = await users.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return memberships
            .Where(m => usersById.ContainsKey(m.UserId))
            .Select(m => new OrgMemberInfo(
                m.UserId, usersById[m.UserId].Email ?? "", usersById[m.UserId].DisplayName, m.Role))
            .OrderBy(m => m.Email)
            .ToList();
    }

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

    public async Task<AccessRequestCreateResult> RequestOrganizationAccessAsync(
        Guid requesterId, Guid organizationId, OrgRole requestedRole, CancellationToken ct = default)
    {
        // Can't request an org you're already in.
        if (await db.OrganizationMemberships.AnyAsync(
                m => m.UserId == requesterId && m.OrganizationId == organizationId, ct))
            return new AccessRequestCreateResult(AccessRequestCreateOutcome.AlreadyMember, Guid.Empty);

        // No duplicate open request.
        var open = await db.OrganizationAccessRequests.FirstOrDefaultAsync(
            r => r.RequesterId == requesterId && r.OrganizationId == organizationId
                 && r.Status == AccessRequestStatus.Pending, ct);
        if (open is not null)
            return new AccessRequestCreateResult(AccessRequestCreateOutcome.DuplicateOpenRequest, open.Id);

        var request = new OrganizationAccessRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            OrganizationId = organizationId,
            RequestedRole = requestedRole,
            Status = AccessRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.OrganizationAccessRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return new AccessRequestCreateResult(AccessRequestCreateOutcome.Created, request.Id);
    }

    public async Task<IReadOnlyList<AccessRequestInfo>> ListPendingAccessRequestsAsync(
        Guid organizationId, CancellationToken ct = default)
    {
        var requests = await db.OrganizationAccessRequests
            .Where(r => r.OrganizationId == organizationId && r.Status == AccessRequestStatus.Pending)
            .ToListAsync(ct);
        return await ProjectRequestsAsync(requests, ct);
    }

    public async Task<IReadOnlyList<AccessRequestInfo>> ListAccessRequestsByRequesterAsync(
        Guid requesterId, CancellationToken ct = default)
    {
        var requests = await db.OrganizationAccessRequests
            .Where(r => r.RequesterId == requesterId)
            .ToListAsync(ct);
        return await ProjectRequestsAsync(requests, ct);
    }

    public async Task<AccessRequestInfo?> GetAccessRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await db.OrganizationAccessRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        return request is null ? null : (await ProjectRequestsAsync([request], ct)).Single();
    }

    public async Task<AccessRequestDecisionResult> ApproveAccessRequestAsync(
        Guid requestId, Guid deciderId, OrgRole? role, CancellationToken ct = default)
    {
        var request = await db.OrganizationAccessRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null)
            return new AccessRequestDecisionResult(AccessRequestDecisionOutcome.NotFound, null);

        var grantRole = role ?? request.RequestedRole;

        // Grant membership (idempotent upsert) and stamp the decision in the SAME SaveChanges, so an
        // approved request always corresponds to a real membership — same context, one transaction.
        var membership = await db.OrganizationMemberships.SingleOrDefaultAsync(
            m => m.UserId == request.RequesterId && m.OrganizationId == request.OrganizationId, ct);
        if (membership is null)
            db.OrganizationMemberships.Add(new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                UserId = request.RequesterId,
                OrganizationId = request.OrganizationId,
                Role = grantRole,
            });
        else
            membership.Role = grantRole;

        request.Status = AccessRequestStatus.Approved;
        request.DecidedAt = DateTimeOffset.UtcNow;
        request.DecidedByUserId = deciderId;
        await db.SaveChangesAsync(ct);

        var info = (await ProjectRequestsAsync([request], ct)).Single();
        return new AccessRequestDecisionResult(AccessRequestDecisionOutcome.Approved, info);
    }

    public async Task<AccessRequestDecisionResult> DenyAccessRequestAsync(
        Guid requestId, Guid deciderId, CancellationToken ct = default)
    {
        var request = await db.OrganizationAccessRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null)
            return new AccessRequestDecisionResult(AccessRequestDecisionOutcome.NotFound, null);
        // A grant already made can't be un-made by a deny — the owner would remove the member instead.
        if (request.Status == AccessRequestStatus.Approved)
            return new AccessRequestDecisionResult(AccessRequestDecisionOutcome.AlreadyApproved, null);

        request.Status = AccessRequestStatus.Denied;
        request.DecidedAt = DateTimeOffset.UtcNow;
        request.DecidedByUserId = deciderId;
        await db.SaveChangesAsync(ct);

        var info = (await ProjectRequestsAsync([request], ct)).Single();
        return new AccessRequestDecisionResult(AccessRequestDecisionOutcome.Denied, info);
    }

    // Projects requests with their requester's email/name, newest first. One users lookup, in-memory join.
    private async Task<IReadOnlyList<AccessRequestInfo>> ProjectRequestsAsync(
        List<OrganizationAccessRequest> requests, CancellationToken ct)
    {
        var userIds = requests.Select(r => r.RequesterId).Distinct().ToList();
        var usersById = await users.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return requests
            .Select(r =>
            {
                usersById.TryGetValue(r.RequesterId, out var u);
                return new AccessRequestInfo(
                    r.Id, r.RequesterId, u?.Email ?? "", u?.DisplayName ?? "",
                    r.OrganizationId, r.RequestedRole, r.Status, r.CreatedAt);
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    private static UserAccount? Project(AppUser? user)
        => user is null ? null : new UserAccount(user.Id, user.Email ?? "", user.DisplayName);
}
