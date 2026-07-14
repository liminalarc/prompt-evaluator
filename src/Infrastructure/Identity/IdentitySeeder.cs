using Application.Identity;
using Application.Ports;

namespace Infrastructure.Identity;

/// <summary>
/// Seeds a bootstrap admin account and grants it the seeded "Default" organization (4.1). This is
/// the first-run escape hatch: without it, a freshly migrated database would have prompts/orgs but
/// no user able to reach them. Idempotent — safe to run on every startup. Works through the
/// <see cref="IUserDirectory"/> port so it stays storage-agnostic and unit-testable.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>The "Default" organization seeded by the AddOrganizations migration.</summary>
    public static readonly Guid DefaultOrganizationId = new("11111111-1111-1111-1111-111111111111");

    public static async Task SeedBootstrapAdminAsync(
        IUserDirectory users,
        string email,
        string displayName,
        string password,
        Guid? organizationId = null,
        CancellationToken ct = default)
    {
        var orgId = organizationId ?? DefaultOrganizationId;

        var existing = await users.FindByEmailAsync(email, ct);
        Guid userId;
        if (existing is null)
        {
            var result = await users.RegisterAsync(email, displayName, password, ct);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Failed to seed bootstrap admin: " + string.Join("; ", result.Errors));
            userId = result.UserId;
        }
        else
        {
            userId = existing.Id;
        }

        await users.GrantOrganizationAsync(userId, orgId, OrgRole.Owner, ct);
    }
}
