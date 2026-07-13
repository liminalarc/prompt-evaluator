using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

public sealed class FolderRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private EvalDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EvalDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new EvalDbContext(options);
    }

    private async Task MigrateAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
    }

    [Fact]
    public async Task Add_then_GetById_round_trips_a_folder_and_its_parent()
    {
        await MigrateAsync();
        var root = Folder.CreateRoot("Stormboard");
        var child = Folder.CreateChild("Summarization", root.Id);

        await using (var write = NewContext())
        {
            var repo = new FolderRepository(write);
            await repo.AddAsync(root);
            await repo.AddAsync(child);
        }

        await using var read = NewContext();
        var loaded = await new FolderRepository(read).GetByIdAsync(child.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Summarization", loaded!.Name);
        Assert.Equal(root.Id, loaded.ParentId);
        Assert.False(loaded.IsTopLevel);
    }

    [Fact]
    public async Task GetTopLevelAncestorId_resolves_the_permission_boundary_at_any_depth()
    {
        await MigrateAsync();
        var root = Folder.CreateRoot("Stormboard");
        var child = Folder.CreateChild("Summarization", root.Id);
        var grandchild = Folder.CreateChild("Threads", child.Id);

        await using (var write = NewContext())
        {
            var repo = new FolderRepository(write);
            await repo.AddAsync(root);
            await repo.AddAsync(child);
            await repo.AddAsync(grandchild);
        }

        await using var read = NewContext();
        var repoRead = new FolderRepository(read);

        // The top-level ancestor (4.1 permission boundary) is the same root at every depth.
        Assert.Equal(root.Id, await repoRead.GetTopLevelAncestorIdAsync(grandchild.Id));
        Assert.Equal(root.Id, await repoRead.GetTopLevelAncestorIdAsync(child.Id));
        // A top-level folder is its own boundary.
        Assert.Equal(root.Id, await repoRead.GetTopLevelAncestorIdAsync(root.Id));
    }

    [Fact]
    public async Task GetTopLevelAncestorId_returns_null_for_an_unknown_folder()
    {
        await MigrateAsync();
        await using var read = NewContext();

        var result = await new FolderRepository(read).GetTopLevelAncestorIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Moving_a_subtree_changes_its_top_level_ancestor()
    {
        await MigrateAsync();
        var oldRoot = Folder.CreateRoot("Old");
        var newRoot = Folder.CreateRoot("New");
        var child = Folder.CreateChild("Child", oldRoot.Id);

        await using (var write = NewContext())
        {
            var repo = new FolderRepository(write);
            await repo.AddAsync(oldRoot);
            await repo.AddAsync(newRoot);
            await repo.AddAsync(child);
        }

        await using (var edit = NewContext())
        {
            var repo = new FolderRepository(edit);
            var loaded = await repo.GetByIdAsync(child.Id);
            loaded!.MoveTo(newRoot.Id);
            await repo.SaveChangesAsync();
        }

        await using var read = NewContext();
        Assert.Equal(newRoot.Id, await new FolderRepository(read).GetTopLevelAncestorIdAsync(child.Id));
    }

    [Fact]
    public async Task Prompts_list_by_folder_and_unfiled_prompts_list_under_the_root()
    {
        await MigrateAsync();
        var folder = Folder.CreateRoot("Stormboard");
        var filed = Prompt.Create("Filed", folderId: folder.Id);
        var unfiled = Prompt.Create("Unfiled");

        await using (var write = NewContext())
        {
            await new FolderRepository(write).AddAsync(folder);
            var prompts = new PromptRepository(write);
            await prompts.AddAsync(filed);
            await prompts.AddAsync(unfiled);
        }

        await using var read = NewContext();
        var repo = new PromptRepository(read);

        var inFolder = await repo.ListByFolderAsync(folder.Id);
        Assert.Equal(filed.Id, Assert.Single(inFolder).Id);

        var root = await repo.ListByFolderAsync(null);
        Assert.Equal(unfiled.Id, Assert.Single(root).Id);
    }

    [Fact]
    public async Task Datasets_list_by_their_owning_prompt()
    {
        await MigrateAsync();
        var promptA = Prompt.Create("A");
        var promptB = Prompt.Create("B");
        var dsA = Dataset.Create(promptA.Id, "A-data");
        var dsB = Dataset.Create(promptB.Id, "B-data");

        await using (var write = NewContext())
        {
            var prompts = new PromptRepository(write);
            await prompts.AddAsync(promptA);
            await prompts.AddAsync(promptB);
            var datasets = new DatasetRepository(write);
            await datasets.AddAsync(dsA);
            await datasets.AddAsync(dsB);
        }

        await using var read = NewContext();
        var forA = await new DatasetRepository(read).ListByPromptAsync(promptA.Id);

        Assert.Equal(dsA.Id, Assert.Single(forA).Id);
    }
}
