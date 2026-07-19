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

            // Grant the Default org ONLY on first creation. The Default org is now an ordinary,
            // deletable org (2.21) — if it's later deleted, a subsequent startup must not resurrect
            // the membership (which would leave the admin pointing at a non-existent org). The seed
            // is a first-run escape hatch, not a standing enforcement.
            await users.GrantOrganizationAsync(userId, orgId, OrgRole.Owner, ct);
        }
        else
        {
            userId = existing.Id;
        }

        // The bootstrap admin is a workspace-level global admin (1.13) so it can manage the Model
        // Catalog on a freshly deployed app. Idempotent (no-op when already set) — kept unconditional
        // so the flag is always ensured, independent of the (deletable) Default org.
        await users.SetGlobalAdminAsync(userId, true, ct);
    }
}
