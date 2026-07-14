using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class OrganizationsEndpointTests : IAsyncLifetime
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

    private sealed record OrgDto(Guid Id, string Name);

    [Fact]
    public async Task Switcher_lists_only_orgs_the_user_belongs_to()
    {
        // A freshly registered user is a member of nothing — not even the seeded "Default" org (4.1).
        var client = await _factory.CreateAuthenticatedClientAsync();
        var before = await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.Empty(before!);
        Assert.DoesNotContain(before!, o => o.Name == "Default");

        // Creating an org grants the creator membership, so it appears in their switcher.
        var created = await (await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" }))
            .Content.ReadFromJsonAsync<OrgDto>();
        var after = await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.Contains(after!, o => o.Id == created!.Id && o.Name == "Acme");
        Assert.DoesNotContain(after!, o => o.Name == "Default");
    }

    [Fact]
    public async Task Create_then_list_includes_the_new_org()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<OrgDto>();

        var orgs = await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.Contains(orgs!, o => o.Id == created!.Id && o.Name == "Acme");
    }

    [Fact]
    public async Task Rename_an_org()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" }))
            .Content.ReadFromJsonAsync<OrgDto>();

        var res = await client.PutAsJsonAsync($"/api/organizations/{created!.Id}", new { name = "Acme Inc" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var renamed = await res.Content.ReadFromJsonAsync<OrgDto>();
        Assert.Equal("Acme Inc", renamed!.Name);
    }

    [Fact]
    public async Task Create_with_a_blank_name_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.PostAsJsonAsync("/api/organizations", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Rename_a_missing_org_returns_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.PutAsJsonAsync($"/api/organizations/{Guid.NewGuid()}", new { name = "X" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed record PromptDto(Guid Id, Guid? FolderId, string Name);

    [Fact]
    public async Task Delete_removes_the_org_and_cascades_to_its_prompts()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var org = await (await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" }))
            .Content.ReadFromJsonAsync<OrgDto>();
        var prompt = await (await client.PostAsJsonAsync($"/api/organizations/{org!.Id}/prompts",
            new { name = "Summarizer", description = (string?)null }))
            .Content.ReadFromJsonAsync<PromptDto>();

        var del = await client.DeleteAsync($"/api/organizations/{org.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // The org is gone from the list, and its prompt cascaded away.
        var orgs = await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.DoesNotContain(orgs!, o => o.Id == org.Id);
        var get = await client.GetAsync($"/api/prompts/{prompt!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_a_missing_org_is_a_no_op_204()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.DeleteAsync($"/api/organizations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_see_or_reach_another_users_org()
    {
        // User A creates org X and a prompt in it.
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var orgX = await (await alice.PostAsJsonAsync("/api/organizations", new { name = "X" }))
            .Content.ReadFromJsonAsync<OrgDto>();
        var prompt = await (await alice.PostAsJsonAsync($"/api/organizations/{orgX!.Id}/prompts",
            new { name = "Summarizer", description = (string?)null }))
            .Content.ReadFromJsonAsync<PromptDto>();

        // User B is a separate account with no membership of org X.
        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");

        // B can't browse X's prompts, nor read A's prompt by id.
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/organizations/{orgX.Id}/prompts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/prompts/{prompt!.Id}")).StatusCode);

        // And X does not appear in B's switcher.
        var bobsOrgs = await bob.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.DoesNotContain(bobsOrgs!, o => o.Id == orgX.Id);
    }
}
