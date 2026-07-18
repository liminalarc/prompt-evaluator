using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class AdminUsersEndpointTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@test.local";
    // The Default org seeded by the AddOrganizations migration (bootstrap admin is granted it).
    private static readonly Guid DefaultOrgId = new("11111111-1111-1111-1111-111111111111");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class AdminFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.UseSetting("Auth:BootstrapAdmin:Email", AdminEmail);
            builder.UseSetting("Auth:BootstrapAdmin:Password", AuthenticationTestExtensions.DefaultPassword);
            builder.UseSetting("Auth:BootstrapAdmin:DisplayName", "Admin");
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new AdminFactory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed record MembershipDto(Guid OrganizationId, string Role);
    private sealed record UserDetailDto(
        Guid Id, string Email, string DisplayName, bool IsAdmin, List<MembershipDto> Memberships);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = AuthenticationTestExtensions.DefaultPassword });
        res.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<List<UserDetailDto>> ListAsync(HttpClient admin)
        => (await admin.GetFromJsonAsync<List<UserDetailDto>>("/api/admin/users"))!;

    private static async Task<Guid> UserIdAsync(HttpClient admin, string email)
        => (await ListAsync(admin)).Single(u => u.Email == email).Id;

    [Fact]
    public async Task Admin_lists_users_with_their_admin_flag_and_memberships()
    {
        await _factory.CreateAuthenticatedClientAsync("member@test.local");
        var admin = await AdminClientAsync();

        var users = await ListAsync(admin);

        var adminRow = Assert.Single(users, u => u.Email == AdminEmail);
        Assert.True(adminRow.IsAdmin);
        Assert.Contains(adminRow.Memberships, m => m.OrganizationId == DefaultOrgId && m.Role == "Owner");
        var memberRow = Assert.Single(users, u => u.Email == "member@test.local");
        Assert.False(memberRow.IsAdmin);
        Assert.Empty(memberRow.Memberships);
    }

    [Fact]
    public async Task Admin_can_create_a_user_who_appears_in_the_list_and_can_log_in()
    {
        var admin = await AdminClientAsync();

        var create = await admin.PostAsJsonAsync("/api/admin/users",
            new { email = "created@test.local", displayName = "Created", password = "Created-Pass-1" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<UserDetailDto>())!;
        Assert.Equal("created@test.local", created.Email);
        Assert.False(created.IsAdmin);
        Assert.Empty(created.Memberships);

        Assert.Contains(await ListAsync(admin), u => u.Email == "created@test.local");

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "created@test.local", password = "Created-Pass-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_create_a_user()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var res = await member.PostAsJsonAsync("/api/admin/users",
            new { email = "nope@test.local", displayName = "Nope", password = "Created-Pass-1" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Creating_a_user_with_a_duplicate_email_is_rejected()
    {
        var admin = await AdminClientAsync();
        await admin.PostAsJsonAsync("/api/admin/users",
            new { email = "dupe@test.local", displayName = "Dupe", password = "Created-Pass-1" });

        var again = await admin.PostAsJsonAsync("/api/admin/users",
            new { email = "dupe@test.local", displayName = "Dupe Two", password = "Created-Pass-1" });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Creating_a_user_with_a_weak_password_is_rejected()
    {
        var admin = await AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/admin/users",
            new { email = "weak@test.local", displayName = "Weak", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_list_users()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var res = await member.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_grant_and_revoke_global_admin()
    {
        await _factory.CreateAuthenticatedClientAsync("member@test.local");
        var admin = await AdminClientAsync();
        var memberId = await UserIdAsync(admin, "member@test.local");

        var grant = await admin.PostAsJsonAsync($"/api/admin/users/{memberId}/admin", new { isAdmin = true });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.True((await ListAsync(admin)).Single(u => u.Id == memberId).IsAdmin);

        var revoke = await admin.PostAsJsonAsync($"/api/admin/users/{memberId}/admin", new { isAdmin = false });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.False((await ListAsync(admin)).Single(u => u.Id == memberId).IsAdmin);
    }

    [Fact]
    public async Task The_last_global_admin_cannot_be_demoted()
    {
        var admin = await AdminClientAsync();
        var adminId = await UserIdAsync(admin, AdminEmail);

        var res = await admin.PostAsJsonAsync($"/api/admin/users/{adminId}/admin", new { isAdmin = false });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True((await ListAsync(admin)).Single(u => u.Id == adminId).IsAdmin); // still admin
    }

    [Fact]
    public async Task Admin_can_grant_and_revoke_org_membership_with_a_role()
    {
        await _factory.CreateAuthenticatedClientAsync("member@test.local");
        var admin = await AdminClientAsync();
        var memberId = await UserIdAsync(admin, "member@test.local");

        var grant = await admin.PostAsJsonAsync($"/api/admin/users/{memberId}/organizations",
            new { organizationId = DefaultOrgId, role = "Member" });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Contains((await ListAsync(admin)).Single(u => u.Id == memberId).Memberships,
            m => m.OrganizationId == DefaultOrgId && m.Role == "Member");

        var revoke = await admin.DeleteAsync($"/api/admin/users/{memberId}/organizations/{DefaultOrgId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Empty((await ListAsync(admin)).Single(u => u.Id == memberId).Memberships);
    }

    [Fact]
    public async Task Admin_set_password_lets_the_user_log_in_with_the_new_password()
    {
        await _factory.CreateAuthenticatedClientAsync("member@test.local");
        var admin = await AdminClientAsync();
        var memberId = await UserIdAsync(admin, "member@test.local");

        var set = await admin.PostAsJsonAsync($"/api/admin/users/{memberId}/password",
            new { newPassword = "New-Password-1" });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var newLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "member@test.local", password = "New-Password-1" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "member@test.local", password = AuthenticationTestExtensions.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task A_user_can_change_their_own_password()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var change = await member.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = AuthenticationTestExtensions.DefaultPassword, newPassword = "Changed-Password-1" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "member@test.local", password = "Changed-Password-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_a_wrong_current_password_is_rejected()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var change = await member.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "not-my-password", newPassword = "Changed-Password-1" });

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
    }
}
