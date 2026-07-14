using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class FoldersEndpointTests : IAsyncLifetime
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

    private sealed record IdName(Guid Id, string Name);
    private sealed record FolderDto(Guid Id, Guid? ParentId, string Name);
    private sealed record PromptDto(Guid Id, Guid? FolderId, string Name);
    private sealed record PromptSummaryDto(Guid Id, Guid? FolderId, string Name);

    private static async Task<Guid> CreateOrgAsync(HttpClient client, string name = "Acme")
    {
        var res = await client.PostAsJsonAsync("/api/organizations", new { name });
        return (await res.Content.ReadFromJsonAsync<IdName>())!.Id;
    }

    private static async Task<Guid> CreatePromptAsync(HttpClient client, Guid orgId, string name)
    {
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts", new { name, description = (string?)null });
        return (await res.Content.ReadFromJsonAsync<PromptDto>())!.Id;
    }

    private static async Task<FolderDto> CreateFolderAsync(HttpClient client, Guid orgId, string name, Guid? parentId = null)
    {
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/folders", new { name, parentId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<FolderDto>())!;
    }

    [Fact]
    public async Task Create_root_and_child_then_fetch_the_org_tree()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);

        var root = await CreateFolderAsync(client, orgId, "Stormboard");
        var child = await CreateFolderAsync(client, orgId, "Summarization", root.Id);

        var tree = await client.GetFromJsonAsync<List<FolderDto>>($"/api/organizations/{orgId}/folders");

        Assert.Equal(2, tree!.Count);
        Assert.Contains(tree, f => f.Id == root.Id && f.ParentId == null);
        Assert.Contains(tree, f => f.Id == child.Id && f.ParentId == root.Id);
    }

    [Fact]
    public async Task Create_child_under_a_missing_parent_returns_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/folders", new { name = "Orphan", parentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Create_in_an_organization_the_user_cannot_access_returns_403()
    {
        // The org membership gate runs before any work (4.1), so a caller who isn't a member of the
        // target org is forbidden — and a non-member can't distinguish a missing org from a foreign one.
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await client.PostAsJsonAsync($"/api/organizations/{Guid.NewGuid()}/folders", new { name = "X", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_blank_name_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/folders", new { name = "   ", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Rename_a_folder()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var folder = await CreateFolderAsync(client, orgId, "Stormbaord");

        var res = await client.PutAsJsonAsync($"/api/folders/{folder.Id}", new { name = "Stormboard" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var tree = await client.GetFromJsonAsync<List<FolderDto>>($"/api/organizations/{orgId}/folders");
        Assert.Equal("Stormboard", Assert.Single(tree!).Name);
    }

    [Fact]
    public async Task Move_a_folder_under_a_new_parent()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var a = await CreateFolderAsync(client, orgId, "A");
        var b = await CreateFolderAsync(client, orgId, "B");
        var child = await CreateFolderAsync(client, orgId, "Child", a.Id);

        var res = await client.PostAsJsonAsync($"/api/folders/{child.Id}/move", new { parentId = b.Id });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var moved = await res.Content.ReadFromJsonAsync<FolderDto>();
        Assert.Equal(b.Id, moved!.ParentId);
    }

    [Fact]
    public async Task Move_a_folder_under_its_own_descendant_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var root = await CreateFolderAsync(client, orgId, "Root");
        var child = await CreateFolderAsync(client, orgId, "Child", root.Id);

        var res = await client.PostAsJsonAsync($"/api/folders/{root.Id}/move", new { parentId = child.Id });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Move_a_prompt_into_a_folder_then_list_it_by_folder()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var folder = await CreateFolderAsync(client, orgId, "Stormboard");
        var promptId = await CreatePromptAsync(client, orgId, "Summarizer");

        var move = await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = folder.Id });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var moved = await move.Content.ReadFromJsonAsync<PromptDto>();
        Assert.Equal(folder.Id, moved!.FolderId);

        var inFolder = await client.GetFromJsonAsync<List<PromptSummaryDto>>($"/api/folders/{folder.Id}/prompts");
        Assert.Equal(promptId, Assert.Single(inFolder!).Id);
    }

    [Fact]
    public async Task Move_a_prompt_into_a_folder_in_a_different_organization_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgA = await CreateOrgAsync(client, "A");
        var orgB = await CreateOrgAsync(client, "B");
        var promptId = await CreatePromptAsync(client, orgA, "Summarizer");
        var foreignFolder = await CreateFolderAsync(client, orgB, "Elsewhere");

        var res = await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = foreignFolder.Id });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Move_a_prompt_into_a_missing_folder_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var promptId = await CreatePromptAsync(client, orgId, "Summarizer");

        var res = await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
