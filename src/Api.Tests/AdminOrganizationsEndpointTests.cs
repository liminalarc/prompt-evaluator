using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Spec 4.4 — the global-admin organization-management surface (<c>/api/admin/organizations</c>):
/// list all orgs with member counts, create/rename/delete (cascade), and drill into an org's
/// members (list/add/remove). Every endpoint is gated by the 1.13 global-admin flag; a non-admin
/// gets 403. Mirrors <see cref="AdminUsersEndpointTests"/>.
/// </summary>
public sealed class AdminOrganizationsEndpointTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@test.local";
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

    private sealed record OrgDto(Guid Id, string Name, int MemberCount);
    private sealed record OrgMemberDto(Guid UserId, string Email, string DisplayName, string Role);
    private sealed record UserRowDto(Guid Id, string Email);
    private sealed record MembershipDto(Guid OrganizationId, string Role);
    private sealed record UserDetailDto(Guid Id, string Email, IReadOnlyList<MembershipDto> Memberships);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = AuthenticationTestExtensions.DefaultPassword });
        res.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<List<OrgDto>> ListOrgsAsync(HttpClient admin)
        => (await admin.GetFromJsonAsync<List<OrgDto>>("/api/admin/organizations"))!;

    private static async Task<Guid> UserIdAsync(HttpClient admin, string email)
    {
        var users = (await admin.GetFromJsonAsync<List<UserRowDto>>("/api/admin/users"))!;
        return users.Single(u => u.Email == email).Id;
    }

    [Fact]
    public async Task Admin_lists_all_orgs_with_member_counts()
    {
        var admin = await AdminClientAsync();

        var orgs = await ListOrgsAsync(admin);

        // The seeded Default org has the bootstrap admin as its sole Owner.
        var defaultOrg = Assert.Single(orgs, o => o.Id == DefaultOrgId);
        Assert.Equal(1, defaultOrg.MemberCount);
    }

    [Fact]
    public async Task Non_admin_cannot_list_orgs()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var res = await member.GetAsync("/api/admin/organizations");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_rename_and_delete_an_org()
    {
        var admin = await AdminClientAsync();

        var create = await admin.PostAsJsonAsync("/api/admin/organizations", new { name = "Acme" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<OrgDto>())!;
        Assert.Equal("Acme", created.Name);
        Assert.Equal(0, created.MemberCount);

        var rename = await admin.PutAsJsonAsync($"/api/admin/organizations/{created.Id}", new { name = "Acme Corp" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal("Acme Corp", (await ListOrgsAsync(admin)).Single(o => o.Id == created.Id).Name);

        var delete = await admin.DeleteAsync($"/api/admin/organizations/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(await ListOrgsAsync(admin), o => o.Id == created.Id);
    }

    [Fact]
    public async Task The_seeded_Default_org_can_be_deleted_like_any_other()
    {
        var admin = await AdminClientAsync();
        Assert.Contains(await ListOrgsAsync(admin), o => o.Id == DefaultOrgId);

        var delete = await admin.DeleteAsync($"/api/admin/organizations/{DefaultOrgId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(await ListOrgsAsync(admin), o => o.Id == DefaultOrgId);

        // The admin's membership to the now-deleted Default org is revoked — no dangling row (2.21).
        var adminId = await UserIdAsync(admin, AdminEmail);
        var users = (await admin.GetFromJsonAsync<List<UserDetailDto>>("/api/admin/users"))!;
        Assert.DoesNotContain(users.Single(u => u.Id == adminId).Memberships, m => m.OrganizationId == DefaultOrgId);
    }

    [Fact]
    public async Task Deleting_an_org_revokes_its_members_leaving_no_orphan_membership()
    {
        await _factory.CreateAuthenticatedClientAsync("member@test.local");
        var admin = await AdminClientAsync();
        var memberId = await UserIdAsync(admin, "member@test.local");

        var created = (await (await admin.PostAsJsonAsync("/api/admin/organizations", new { name = "Ephemeral" }))
            .Content.ReadFromJsonAsync<OrgDto>())!;
        var add = await admin.PostAsJsonAsync($"/api/admin/organizations/{created.Id}/members",
            new { userId = memberId, role = "Member" });
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        var delete = await admin.DeleteAsync($"/api/admin/organizations/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var users = (await admin.GetFromJsonAsync<List<UserDetailDto>>("/api/admin/users"))!;
        var member = users.Single(u => u.Id == memberId);
        Assert.DoesNotContain(member.Memberships, m => m.OrganizationId == created.Id);
    }

    [Fact]
    public async Task Creating_an_org_with_a_blank_name_is_rejected()
    {
        var admin = await AdminClientAsync();

        var res = await admin.PostAsJsonAsync("/api/admin/organizations", new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_add_and_remove_org_members()
    {
        // Register a plain user, then an admin adds them to a new org and removes them again.
        await _factory.CreateAuthenticatedClientAsync("member@test.local");
        var admin = await AdminClientAsync();
        var memberId = await UserIdAsync(admin, "member@test.local");

        var created = (await (await admin.PostAsJsonAsync("/api/admin/organizations", new { name = "Beta" }))
            .Content.ReadFromJsonAsync<OrgDto>())!;

        var add = await admin.PostAsJsonAsync($"/api/admin/organizations/{created.Id}/members",
            new { userId = memberId, role = "Member" });
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        var members = (await admin.GetFromJsonAsync<List<OrgMemberDto>>(
            $"/api/admin/organizations/{created.Id}/members"))!;
        var row = Assert.Single(members, m => m.UserId == memberId);
        Assert.Equal("member@test.local", row.Email);
        Assert.Equal("Member", row.Role);

        // The count endpoint reflects the new membership.
        Assert.Equal(1, (await ListOrgsAsync(admin)).Single(o => o.Id == created.Id).MemberCount);

        var remove = await admin.DeleteAsync($"/api/admin/organizations/{created.Id}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<OrgMemberDto>>(
            $"/api/admin/organizations/{created.Id}/members"))!);
    }

    [Fact]
    public async Task Non_admin_cannot_manage_org_members()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var list = await member.GetAsync($"/api/admin/organizations/{DefaultOrgId}/members");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var add = await member.PostAsJsonAsync($"/api/admin/organizations/{DefaultOrgId}/members",
            new { userId = Guid.NewGuid(), role = "Member" });
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }
}
