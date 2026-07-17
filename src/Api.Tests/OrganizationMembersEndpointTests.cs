using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Spec 4.5 — the owner-facing member-management surface on the member-scoped
/// <c>/api/organizations/{id}/members</c> endpoints. The permission model is **owner-or-admin, per
/// org**: an org's <c>Owner</c> (or a global admin) can list/add/remove members and set roles; a
/// plain member or non-member gets 403. Distinct from 4.4's global-admin-only
/// <c>/api/admin/organizations</c> surface. Members are added **by email** (an owner isn't an admin
/// and can't enumerate the user directory). A last-owner guard blocks demoting/removing an org's
/// final Owner via these endpoints (a global admin escapes via the 4.4 admin surface).
/// </summary>
public sealed class OrganizationMembersEndpointTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@test.local";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
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
        _factory = new Factory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed record OrgDto(Guid Id, string Name, string Role);
    private sealed record MemberDto(Guid UserId, string Email, string DisplayName, string Role);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = AuthenticationTestExtensions.DefaultPassword });
        res.EnsureSuccessStatusCode();
        return client;
    }

    // A user who creates an org becomes its Owner (4.1); returns the new org.
    private static async Task<OrgDto> CreateOrgAsync(HttpClient owner, string name)
        => (await (await owner.PostAsJsonAsync("/api/organizations", new { name })).Content
            .ReadFromJsonAsync<OrgDto>())!;

    private static Task<List<MemberDto>?> ListMembersAsync(HttpClient client, Guid orgId)
        => client.GetFromJsonAsync<List<MemberDto>>($"/api/organizations/{orgId}/members");

    [Fact]
    public async Task Org_list_reports_the_callers_role()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var org = await CreateOrgAsync(alice, "Acme");

        var orgs = (await alice.GetFromJsonAsync<List<OrgDto>>("/api/organizations"))!;

        Assert.Equal("Owner", orgs.Single(o => o.Id == org.Id).Role);
    }

    [Fact]
    public async Task Owner_lists_members_with_roles()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var org = await CreateOrgAsync(alice, "Acme");

        var members = (await ListMembersAsync(alice, org.Id))!;

        var row = Assert.Single(members);
        Assert.Equal("alice@test.local", row.Email);
        Assert.Equal("Owner", row.Role);
    }

    [Fact]
    public async Task Owner_can_add_remove_and_set_a_members_role_by_email()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        await _factory.CreateAuthenticatedClientAsync("bob@test.local"); // bob self-registers
        var org = await CreateOrgAsync(alice, "Acme");

        // Add bob by email as a Member.
        var add = await alice.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new { email = "bob@test.local", role = "Member" });
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);
        var bob = Assert.Single((await ListMembersAsync(alice, org.Id))!, m => m.Email == "bob@test.local");
        Assert.Equal("Member", bob.Role);

        // Promote bob to Owner.
        var setRole = await alice.PutAsJsonAsync(
            $"/api/organizations/{org.Id}/members/{bob.UserId}", new { role = "Owner" });
        Assert.Equal(HttpStatusCode.NoContent, setRole.StatusCode);
        Assert.Equal("Owner",
            (await ListMembersAsync(alice, org.Id))!.Single(m => m.UserId == bob.UserId).Role);

        // Remove bob.
        var remove = await alice.DeleteAsync($"/api/organizations/{org.Id}/members/{bob.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.DoesNotContain((await ListMembersAsync(alice, org.Id))!, m => m.UserId == bob.UserId);
    }

    [Fact]
    public async Task Adding_a_member_by_unknown_email_returns_400()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var org = await CreateOrgAsync(alice, "Acme");

        var res = await alice.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new { email = "nobody@test.local", role = "Member" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_plain_member_cannot_manage_members()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        var org = await CreateOrgAsync(alice, "Acme");
        (await alice.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new { email = "bob@test.local", role = "Member" })).EnsureSuccessStatusCode();

        // Bob is a member but not an Owner — no management.
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/organizations/{org.Id}/members")).StatusCode);
        var add = await bob.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new { email = "alice@test.local", role = "Member" });
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_manage_members()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var carol = await _factory.CreateAuthenticatedClientAsync("carol@test.local");
        var org = await CreateOrgAsync(alice, "Acme");

        Assert.Equal(HttpStatusCode.Forbidden, (await carol.GetAsync($"/api/organizations/{org.Id}/members")).StatusCode);
    }

    [Fact]
    public async Task A_global_admin_can_manage_any_orgs_members()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var org = await CreateOrgAsync(alice, "Acme");
        var admin = await AdminClientAsync(); // not a member of Acme, but a global admin

        var members = (await ListMembersAsync(admin, org.Id))!;

        Assert.Single(members, m => m.Email == "alice@test.local" && m.Role == "Owner");
    }

    [Fact]
    public async Task The_last_owner_cannot_be_removed_or_demoted()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        var org = await CreateOrgAsync(alice, "Acme");
        var aliceId = (await ListMembersAsync(alice, org.Id))!.Single().UserId;

        // Alice is the sole Owner — she can neither be demoted nor removed.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await alice.PutAsJsonAsync($"/api/organizations/{org.Id}/members/{aliceId}", new { role = "Member" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await alice.DeleteAsync($"/api/organizations/{org.Id}/members/{aliceId}")).StatusCode);

        // Once a second Owner exists, the guard releases.
        (await alice.PostAsJsonAsync($"/api/organizations/{org.Id}/members",
            new { email = "bob@test.local", role = "Owner" })).EnsureSuccessStatusCode();
        var demote = await alice.PutAsJsonAsync(
            $"/api/organizations/{org.Id}/members/{aliceId}", new { role = "Member" });
        Assert.Equal(HttpStatusCode.NoContent, demote.StatusCode);
    }

    [Fact]
    public async Task Managing_members_of_a_missing_org_returns_404()
    {
        var admin = await AdminClientAsync();

        var res = await admin.GetAsync($"/api/organizations/{Guid.NewGuid()}/members");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
