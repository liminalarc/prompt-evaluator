using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Spec 2.21 — request-to-join access (the pull path). A user requests access to an existing org;
/// its owner (or a workspace admin) sees the pending queue and approves (→ a membership grant) or
/// denies. Covers the lifecycle, the owner-or-admin gate (sibling to 4.5's member endpoints), the
/// discovery directory, and the three domain rules (no duplicate open request, can't request an org
/// you're in, idempotent approve).
/// </summary>
public sealed class OrganizationAccessRequestsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:Postgres", connectionString);
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
    private sealed record CreatedIdDto(Guid Id);
    private sealed record AccessRequestDto(
        Guid Id, Guid RequesterId, string RequesterEmail, string RequesterDisplayName,
        Guid OrganizationId, string RequestedRole, string Status, DateTimeOffset CreatedAt);
    private sealed record DirectoryDto(Guid Id, string Name, bool IsMember, bool HasPendingRequest);

    // An owner who has created an org, plus the org id.
    private async Task<(HttpClient Owner, Guid OrgId)> OwnerWithOrgAsync(string name = "Acme")
    {
        var owner = await _factory.CreateAuthenticatedClientAsync($"owner-{Guid.NewGuid():N}@test.local");
        var org = (await (await owner.PostAsJsonAsync("/api/organizations", new { name }))
            .Content.ReadFromJsonAsync<OrgDto>())!;
        return (owner, org.Id);
    }

    private static async Task<List<OrgDto>> SwitcherAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations"))!;

    [Fact]
    public async Task Request_then_owner_approves_grants_the_requester_membership()
    {
        var (owner, orgId) = await OwnerWithOrgAsync();
        var requester = await _factory.CreateAuthenticatedClientAsync("req@test.local");

        // The requester isn't a member yet.
        Assert.DoesNotContain(await SwitcherAsync(requester), o => o.Id == orgId);

        var create = await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        // The owner sees it pending.
        var pending = (await owner.GetFromJsonAsync<List<AccessRequestDto>>($"/api/organizations/{orgId}/access-requests"))!;
        var row = Assert.Single(pending);
        Assert.Equal("req@test.local", row.RequesterEmail);
        Assert.Equal("Pending", row.Status);

        // The owner approves.
        var approve = await owner.PostAsJsonAsync(
            $"/api/organizations/{orgId}/access-requests/{row.Id}/approve", new { role = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // The requester is now a member; the queue is empty.
        Assert.Contains(await SwitcherAsync(requester), o => o.Id == orgId);
        Assert.Empty((await owner.GetFromJsonAsync<List<AccessRequestDto>>($"/api/organizations/{orgId}/access-requests"))!);
    }

    [Fact]
    public async Task Owner_can_approve_at_an_elevated_role()
    {
        var (owner, orgId) = await OwnerWithOrgAsync();
        var requester = await _factory.CreateAuthenticatedClientAsync("req2@test.local");
        var created = (await (await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" }))
            .Content.ReadFromJsonAsync<CreatedIdDto>())!;

        var approve = await owner.PostAsJsonAsync(
            $"/api/organizations/{orgId}/access-requests/{created.Id}/approve", new { role = "Owner" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // The requester holds Owner in that org (the switcher stamps the caller's role).
        var org = Assert.Single(await SwitcherAsync(requester), o => o.Id == orgId);
        Assert.Equal("Owner", org.Role);
    }

    [Fact]
    public async Task Request_then_owner_denies_leaves_a_non_member_and_clears_the_queue()
    {
        var (owner, orgId) = await OwnerWithOrgAsync();
        var requester = await _factory.CreateAuthenticatedClientAsync("req3@test.local");
        var created = (await (await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" }))
            .Content.ReadFromJsonAsync<CreatedIdDto>())!;

        var deny = await owner.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests/{created.Id}/deny", new { });
        Assert.Equal(HttpStatusCode.OK, deny.StatusCode);

        Assert.DoesNotContain(await SwitcherAsync(requester), o => o.Id == orgId);
        Assert.Empty((await owner.GetFromJsonAsync<List<AccessRequestDto>>($"/api/organizations/{orgId}/access-requests"))!);
    }

    [Fact]
    public async Task Cannot_request_an_org_you_are_already_a_member_of()
    {
        var (owner, orgId) = await OwnerWithOrgAsync();

        // The owner is already a member — requesting their own org is a 400.
        var res = await owner.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_open_request_is_rejected_with_409()
    {
        var (_, orgId) = await OwnerWithOrgAsync();
        var requester = await _factory.CreateAuthenticatedClientAsync("dup@test.local");

        Assert.Equal(HttpStatusCode.Created,
            (await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" })).StatusCode);
    }

    [Fact]
    public async Task Approve_is_idempotent()
    {
        var (owner, orgId) = await OwnerWithOrgAsync();
        var requester = await _factory.CreateAuthenticatedClientAsync("idem@test.local");
        var created = (await (await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" }))
            .Content.ReadFromJsonAsync<CreatedIdDto>())!;

        var first = await owner.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests/{created.Id}/approve", new { });
        var second = await owner.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests/{created.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Still exactly one membership to that org (the switcher lists it once).
        Assert.Single(await SwitcherAsync(requester), o => o.Id == orgId);
    }

    [Fact]
    public async Task A_non_owner_cannot_view_or_decide_requests()
    {
        var (_, orgId) = await OwnerWithOrgAsync();
        var requester = await _factory.CreateAuthenticatedClientAsync("req4@test.local");
        var created = (await (await requester.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests", new { role = "Member" }))
            .Content.ReadFromJsonAsync<CreatedIdDto>())!;

        // A stranger (not owner, not admin) can't list or decide.
        var stranger = await _factory.CreateAuthenticatedClientAsync("stranger@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/organizations/{orgId}/access-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests/{created.Id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.PostAsJsonAsync($"/api/organizations/{orgId}/access-requests/{created.Id}/deny", new { })).StatusCode);
    }

    [Fact]
    public async Task Directory_lists_all_orgs_with_the_callers_membership_and_pending_flags()
    {
        var (_, ownedOrgId) = await OwnerWithOrgAsync("Owned");
        var (_, otherOrgId) = await OwnerWithOrgAsync("Other");
        var requester = await _factory.CreateAuthenticatedClientAsync("dir@test.local");

        // Request one of the two orgs.
        await requester.PostAsJsonAsync($"/api/organizations/{otherOrgId}/access-requests", new { role = "Member" });

        var dir = (await requester.GetFromJsonAsync<List<DirectoryDto>>("/api/organizations/directory"))!;
        // Both orgs are discoverable; the requester is a member of neither but has a pending request to one.
        var other = Assert.Single(dir, e => e.Id == otherOrgId);
        Assert.False(other.IsMember);
        Assert.True(other.HasPendingRequest);
        var owned = Assert.Single(dir, e => e.Id == ownedOrgId);
        Assert.False(owned.IsMember);
        Assert.False(owned.HasPendingRequest);
    }

    [Fact]
    public async Task Requesting_a_missing_org_returns_404()
    {
        var requester = await _factory.CreateAuthenticatedClientAsync("nope@test.local");
        var res = await requester.PostAsJsonAsync($"/api/organizations/{Guid.NewGuid()}/access-requests", new { role = "Member" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
