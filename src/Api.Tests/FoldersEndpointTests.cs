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

    private sealed record FolderDto(Guid Id, Guid? ParentId, string Name);
    private sealed record PromptDto(Guid Id, Guid? FolderId, string Name);
    private sealed record PromptSummaryDto(Guid Id, Guid? FolderId, string Name);

    private static async Task<Guid> CreatePromptAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/prompts", new { name, description = (string?)null });
        return (await res.Content.ReadFromJsonAsync<PromptDto>())!.Id;
    }

    private static async Task<FolderDto> CreateFolderAsync(HttpClient client, string name, Guid? parentId = null)
    {
        var res = await client.PostAsJsonAsync("/api/folders", new { name, parentId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<FolderDto>())!;
    }

    [Fact]
    public async Task Create_root_and_child_then_fetch_the_tree()
    {
        var client = _factory.CreateClient();

        var root = await CreateFolderAsync(client, "Stormboard");
        var child = await CreateFolderAsync(client, "Summarization", root.Id);

        var tree = await client.GetFromJsonAsync<List<FolderDto>>("/api/folders");

        Assert.Equal(2, tree!.Count);
        Assert.Contains(tree, f => f.Id == root.Id && f.ParentId == null);
        Assert.Contains(tree, f => f.Id == child.Id && f.ParentId == root.Id);
    }

    [Fact]
    public async Task Create_child_under_a_missing_parent_returns_404()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/folders", new { name = "Orphan", parentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_blank_name_returns_400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/folders", new { name = "   ", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Rename_a_folder()
    {
        var client = _factory.CreateClient();
        var folder = await CreateFolderAsync(client, "Stormbaord");

        var res = await client.PutAsJsonAsync($"/api/folders/{folder.Id}", new { name = "Stormboard" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var tree = await client.GetFromJsonAsync<List<FolderDto>>("/api/folders");
        Assert.Equal("Stormboard", Assert.Single(tree!).Name);
    }

    [Fact]
    public async Task Move_a_folder_under_a_new_parent()
    {
        var client = _factory.CreateClient();
        var a = await CreateFolderAsync(client, "A");
        var b = await CreateFolderAsync(client, "B");
        var child = await CreateFolderAsync(client, "Child", a.Id);

        var res = await client.PostAsJsonAsync($"/api/folders/{child.Id}/move", new { parentId = b.Id });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var moved = await res.Content.ReadFromJsonAsync<FolderDto>();
        Assert.Equal(b.Id, moved!.ParentId);
    }

    [Fact]
    public async Task Move_a_folder_under_its_own_descendant_returns_400()
    {
        var client = _factory.CreateClient();
        var root = await CreateFolderAsync(client, "Root");
        var child = await CreateFolderAsync(client, "Child", root.Id);

        var res = await client.PostAsJsonAsync($"/api/folders/{root.Id}/move", new { parentId = child.Id });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Move_a_prompt_into_a_folder_then_list_it_by_folder()
    {
        var client = _factory.CreateClient();
        var folder = await CreateFolderAsync(client, "Stormboard");
        var promptId = await CreatePromptAsync(client, "Summarizer");

        var move = await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = folder.Id });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var moved = await move.Content.ReadFromJsonAsync<PromptDto>();
        Assert.Equal(folder.Id, moved!.FolderId);

        var inFolder = await client.GetFromJsonAsync<List<PromptSummaryDto>>($"/api/folders/{folder.Id}/prompts");
        Assert.Equal(promptId, Assert.Single(inFolder!).Id);
    }

    [Fact]
    public async Task Move_a_prompt_to_the_root_unfiles_it()
    {
        var client = _factory.CreateClient();
        var folder = await CreateFolderAsync(client, "Stormboard");
        var promptId = await CreatePromptAsync(client, "Summarizer");
        await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = folder.Id });

        var unfile = await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, unfile.StatusCode);
        var unfiled = await unfile.Content.ReadFromJsonAsync<PromptDto>();
        Assert.Null(unfiled!.FolderId);

        var root = await client.GetFromJsonAsync<List<PromptSummaryDto>>("/api/folders/root/prompts");
        Assert.Contains(root!, p => p.Id == promptId);
    }

    [Fact]
    public async Task Move_a_prompt_into_a_missing_folder_returns_400()
    {
        var client = _factory.CreateClient();
        var promptId = await CreatePromptAsync(client, "Summarizer");

        var res = await client.PostAsJsonAsync($"/api/prompts/{promptId}/move", new { folderId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
