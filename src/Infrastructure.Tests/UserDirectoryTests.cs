using Application.Identity;
using Application.Ports;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

/// <summary>
/// Exercises the Identity bounded context's store (4.1) against a real Postgres: registration,
/// credential validation, per-organization grants, and the idempotent bootstrap-admin seed.
/// </summary>
public sealed class UserDirectoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Bring up the identity schema once for the container.
        var services = BuildProvider();
        await using var db = services.GetRequiredService<AppIdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    // Each call builds a fresh DI scope over the shared container (mirrors the Api composition).
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppIdentityDbContext>(o => o.UseNpgsql(
            _postgres.GetConnectionString(),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history_identity")));
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppIdentityDbContext>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        return services.BuildServiceProvider();
    }

    private async Task<T> WithDirectory<T>(Func<IUserDirectory, Task<T>> act)
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<IUserDirectory>());
    }

    [Fact]
    public async Task Register_then_validate_credentials_round_trips_and_rejects_a_wrong_password()
    {
        var userId = await WithDirectory(async d =>
        {
            var result = await d.RegisterAsync("ada@example.com", "Ada", "Correct-Horse-9");
            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
            return result.UserId;
        });

        Assert.Equal(userId, await WithDirectory(d => d.ValidateCredentialsAsync("ada@example.com", "Correct-Horse-9")));
        Assert.Null(await WithDirectory(d => d.ValidateCredentialsAsync("ada@example.com", "wrong-password")));
        Assert.Null(await WithDirectory(d => d.ValidateCredentialsAsync("nobody@example.com", "Correct-Horse-9")));
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        await WithDirectory(async d =>
        {
            Assert.True((await d.RegisterAsync("dup@example.com", "First", "Correct-Horse-9")).Succeeded);
            var second = await d.RegisterAsync("dup@example.com", "Second", "Another-Strong-9");
            Assert.False(second.Succeeded);
            Assert.NotEmpty(second.Errors);
            return 0;
        });
    }

    [Fact]
    public async Task Granting_organizations_drives_access_queries()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        var userId = await WithDirectory(async d =>
            (await d.RegisterAsync("grace@example.com", "Grace", "Correct-Horse-9")).UserId);

        await WithDirectory(async d =>
        {
            await d.GrantOrganizationAsync(userId, orgA, OrgRole.Owner);
            await d.GrantOrganizationAsync(userId, orgB, OrgRole.Member);
            // Re-granting the same org is an idempotent upsert, not a duplicate row.
            await d.GrantOrganizationAsync(userId, orgA, OrgRole.Member);
            return 0;
        });

        var accessible = await WithDirectory(d => d.GetAccessibleOrganizationIdsAsync(userId));
        Assert.Equal(2, accessible.Count);
        Assert.Contains(orgA, accessible);
        Assert.Contains(orgB, accessible);

        Assert.True(await WithDirectory(d => d.IsMemberAsync(userId, orgA)));
        Assert.False(await WithDirectory(d => d.IsMemberAsync(userId, Guid.NewGuid())));
    }

    [Fact]
    public async Task RemoveAllMembersAsync_revokes_every_membership_of_that_org_only()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        var u1 = await WithDirectory(async d => (await d.RegisterAsync("m1@example.com", "M1", "Correct-Horse-9")).UserId);
        var u2 = await WithDirectory(async d => (await d.RegisterAsync("m2@example.com", "M2", "Correct-Horse-9")).UserId);

        await WithDirectory(async d =>
        {
            await d.GrantOrganizationAsync(u1, orgA, OrgRole.Owner);
            await d.GrantOrganizationAsync(u2, orgA, OrgRole.Member);
            await d.GrantOrganizationAsync(u1, orgB, OrgRole.Owner);
            return 0;
        });

        await WithDirectory(async d => { await d.RemoveAllMembersAsync(orgA); return 0; });

        Assert.False(await WithDirectory(d => d.IsMemberAsync(u1, orgA)));
        Assert.False(await WithDirectory(d => d.IsMemberAsync(u2, orgA)));
        // A different org's memberships are untouched.
        Assert.True(await WithDirectory(d => d.IsMemberAsync(u1, orgB)));
    }

    [Fact]
    public async Task Seeding_does_not_re_grant_the_default_org_after_it_was_removed()
    {
        var org = Guid.NewGuid();

        var adminId = await WithDirectory(async d =>
        {
            await IdentitySeeder.SeedBootstrapAdminAsync(d, "boss@example.com", "Boss", "Correct-Horse-9", org);
            return (await d.FindByEmailAsync("boss@example.com"))!.Id;
        });

        // The Default org is deleted like any other (2.21), taking its memberships with it.
        await WithDirectory(async d => { await d.RemoveAllMembersAsync(org); return 0; });

        // A later startup re-runs the idempotent seeder — it must NOT resurrect the membership.
        await WithDirectory(async d =>
        {
            await IdentitySeeder.SeedBootstrapAdminAsync(d, "boss@example.com", "Boss", "Correct-Horse-9", org);
            return 0;
        });

        Assert.Empty(await WithDirectory(d => d.GetAccessibleOrganizationIdsAsync(adminId)));
    }

    [Fact]
    public async Task Seeding_the_bootstrap_admin_is_idempotent_and_grants_the_default_org()
    {
        var org = Guid.NewGuid();

        await WithDirectory(async d =>
        {
            await IdentitySeeder.SeedBootstrapAdminAsync(d, "admin@example.com", "Admin", "Correct-Horse-9", org);
            await IdentitySeeder.SeedBootstrapAdminAsync(d, "admin@example.com", "Admin", "Correct-Horse-9", org);
            return 0;
        });

        var admin = await WithDirectory(d => d.FindByEmailAsync("admin@example.com"));
        Assert.NotNull(admin);
        Assert.Equal("Admin", admin!.DisplayName);

        var accessible = await WithDirectory(d => d.GetAccessibleOrganizationIdsAsync(admin.Id));
        Assert.Equal(new[] { org }, accessible);
    }
}
