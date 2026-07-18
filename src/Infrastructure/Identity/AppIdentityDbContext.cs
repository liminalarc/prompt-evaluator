using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// The Identity bounded context's store (4.1) — a separate <see cref="DbContext"/> from
/// <see cref="Persistence.EvalDbContext"/> on the same Postgres, with its own migration history.
/// Holds the ASP.NET Core Identity user tables (no global roles — the organization is the boundary)
/// plus the <see cref="OrganizationMembership"/> grants. Kept apart from the domain context so the
/// framework-managed identity schema never entangles domain migrations.
///
/// Also the Data-Protection key store (3.2): keys persist here so the auth cookie stays valid across
/// App Runner replicas (a per-process key ring would reject cookies issued by another instance).
/// </summary>
public sealed class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Only this context's own configurations — NOT the whole assembly, which would drag the
        // domain entities (prompts, organizations, …) into the identity store's model.
        builder.ApplyConfiguration(new Configurations.OrganizationMembershipConfiguration());

        // Snake-case the identity tables to match the rest of the schema.
        builder.Entity<AppUser>().ToTable("users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
