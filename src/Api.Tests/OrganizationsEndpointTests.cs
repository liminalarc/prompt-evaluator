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
    public async Task Seeded_Default_org_is_listed()
    {
        var client = _factory.CreateClient();
        var orgs = await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.Contains(orgs!, o => o.Name == "Default");
    }

    [Fact]
    public async Task Create_then_list_includes_the_new_org()
    {
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<OrgDto>();

        var orgs = await client.GetFromJsonAsync<List<OrgDto>>("/api/organizations");
        Assert.Contains(orgs!, o => o.Id == created!.Id && o.Name == "Acme");
    }

    [Fact]
    public async Task Rename_an_org()
    {
        var client = _factory.CreateClient();
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
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/organizations", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Rename_a_missing_org_returns_404()
    {
        var client = _factory.CreateClient();
        var res = await client.PutAsJsonAsync($"/api/organizations/{Guid.NewGuid()}", new { name = "X" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed record PromptDto(Guid Id, Guid? FolderId, string Name);

    [Fact]
    public async Task Delete_removes_the_org_and_cascades_to_its_prompts()
    {
        var client = _factory.CreateClient();
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
        var client = _factory.CreateClient();
        var res = await client.DeleteAsync($"/api/organizations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
