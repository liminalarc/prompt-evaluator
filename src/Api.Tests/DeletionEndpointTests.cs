using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// The 1.10 delete endpoints for prompts, datasets, and folders: each is org-scoped (a non-member
/// gets 403, a missing entity 404) and cascades / reparents per the decisions. Org delete is
/// already covered by <see cref="OrganizationsEndpointTests"/>.
/// </summary>
public sealed class DeletionEndpointTests : IAsyncLifetime
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
    private sealed record PromptDto(Guid Id, Guid? FolderId, string Name);
    private sealed record DatasetDto(Guid Id, Guid PromptId, string Name);
    private sealed record FolderDto(Guid Id, Guid? ParentId, string Name);

    private static async Task<Guid> CreateOrgAsync(HttpClient client, string name = "Acme")
    {
        var res = await client.PostAsJsonAsync("/api/organizations", new { name });
        return (await res.Content.ReadFromJsonAsync<IdName>())!.Id;
    }

    private static async Task<Guid> CreatePromptAsync(HttpClient client, Guid orgId, string name = "Summarizer")
    {
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts", new { name, description = (string?)null });
        return (await res.Content.ReadFromJsonAsync<PromptDto>())!.Id;
    }

    private static async Task<Guid> CreateDatasetAsync(HttpClient client, Guid promptId, string name = "Data")
    {
        var res = await client.PostAsJsonAsync($"/api/prompts/{promptId}/datasets", new { name, description = (string?)null });
        return (await res.Content.ReadFromJsonAsync<DatasetDto>())!.Id;
    }

    private static async Task<FolderDto> CreateFolderAsync(HttpClient client, Guid orgId, string name, Guid? parentId = null)
    {
        var res = await client.PostAsJsonAsync($"/api/organizations/{orgId}/folders", new { name, parentId });
        return (await res.Content.ReadFromJsonAsync<FolderDto>())!;
    }

    // ---- Prompt delete ----

    [Fact]
    public async Task Delete_prompt_removes_it_and_cascades_its_datasets()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var promptId = await CreatePromptAsync(client, orgId);
        var datasetId = await CreateDatasetAsync(client, promptId);

        var del = await client.DeleteAsync($"/api/prompts/{promptId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/prompts/{promptId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/datasets/{datasetId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_prompt_a_non_member_cannot_reach_is_403()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var orgId = await CreateOrgAsync(alice);
        var promptId = await CreatePromptAsync(alice, orgId);

        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        var del = await bob.DeleteAsync($"/api/prompts/{promptId}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);

        // …and it survives.
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/prompts/{promptId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_missing_prompt_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var del = await client.DeleteAsync($"/api/prompts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    // ---- Dataset delete ----

    [Fact]
    public async Task Delete_dataset_removes_it()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var promptId = await CreatePromptAsync(client, orgId);
        var datasetId = await CreateDatasetAsync(client, promptId);

        var del = await client.DeleteAsync($"/api/datasets/{datasetId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/datasets/{datasetId}")).StatusCode);
        // The owning prompt is untouched.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/prompts/{promptId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_dataset_a_non_member_cannot_reach_is_403()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var orgId = await CreateOrgAsync(alice);
        var promptId = await CreatePromptAsync(alice, orgId);
        var datasetId = await CreateDatasetAsync(alice, promptId);

        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        var del = await bob.DeleteAsync($"/api/datasets/{datasetId}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task Delete_missing_dataset_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var del = await client.DeleteAsync($"/api/datasets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    // ---- Folder delete (reparent) ----

    [Fact]
    public async Task Delete_folder_reparents_children_and_prompts_then_removes_it()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var root = await CreateFolderAsync(client, orgId, "Root");
        var middle = await CreateFolderAsync(client, orgId, "Middle", root.Id);
        var leaf = await CreateFolderAsync(client, orgId, "Leaf", middle.Id);
        var promptId = await CreatePromptAsync(client, orgId, "Filed");
        // File the prompt into the middle folder.
        (await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = middle.Id }))
            .EnsureSuccessStatusCode();

        var del = await client.DeleteAsync($"/api/folders/{middle.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var tree = await client.GetFromJsonAsync<List<FolderDto>>($"/api/organizations/{orgId}/folders");
        Assert.DoesNotContain(tree!, f => f.Id == middle.Id);
        // The child folder is reparented to the deleted folder's parent (root), not deleted.
        Assert.Contains(tree!, f => f.Id == leaf.Id && f.ParentId == root.Id);
        // The prompt moved up with it.
        var prompt = await client.GetFromJsonAsync<PromptDto>($"/api/prompts/{promptId}");
        Assert.Equal(root.Id, prompt!.FolderId);
    }

    [Fact]
    public async Task Delete_folder_a_non_member_cannot_reach_is_403()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var orgId = await CreateOrgAsync(alice);
        var folder = await CreateFolderAsync(alice, orgId, "Secret");

        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        var del = await bob.DeleteAsync($"/api/folders/{folder.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task Delete_missing_folder_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var del = await client.DeleteAsync($"/api/folders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }
}
