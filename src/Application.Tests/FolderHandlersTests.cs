using Application.Folders;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class FolderHandlersTests
{
    private sealed class InMemoryFolderRepo : IFolderRepository
    {
        public readonly List<Folder> Saved = [];

        public Task AddAsync(Folder folder, CancellationToken ct = default) { Saved.Add(folder); return Task.CompletedTask; }
        public Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(f => f.Id == id));
        public Task<IReadOnlyList<Folder>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Folder>>(Saved);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<Guid?> GetTopLevelAncestorIdAsync(Guid folderId, CancellationToken ct = default)
        {
            var current = Saved.SingleOrDefault(f => f.Id == folderId);
            if (current is null) return Task.FromResult<Guid?>(null);
            while (current!.ParentId is Guid pid)
                current = Saved.Single(f => f.Id == pid);
            return Task.FromResult<Guid?>(current.Id);
        }

        public Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken ct = default)
        {
            var result = new List<Guid>();
            var frontier = new Queue<Guid>();
            frontier.Enqueue(folderId);
            while (frontier.Count > 0)
            {
                var parent = frontier.Dequeue();
                foreach (var child in Saved.Where(f => f.ParentId == parent))
                {
                    result.Add(child.Id);
                    frontier.Enqueue(child.Id);
                }
            }
            return Task.FromResult<IReadOnlyList<Guid>>(result);
        }
    }

    private sealed class InMemoryPromptRepo : IPromptRepository
    {
        public readonly List<Prompt> Saved = [];
        public Task AddAsync(Prompt prompt, CancellationToken ct = default) { Saved.Add(prompt); return Task.CompletedTask; }
        public Task<Prompt?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Prompt>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Prompt>>(Saved);
        public Task<IReadOnlyList<Prompt>> ListByFolderAsync(Guid? folderId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Prompt>>(Saved.Where(p => p.FolderId == folderId).ToList());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly Organization Org = Organization.Create("Acme");
    private static readonly Guid OrgId = Org.Id;

    private static CreateFolderHandler NewCreateFolderHandler(InMemoryFolderRepo folders)
        => new(new InMemoryOrganizationRepo(Org), folders);

    // ---- CreateFolderHandler ----

    [Fact]
    public async Task CreateFolder_creates_a_top_level_folder_when_no_parent()
    {
        var repo = new InMemoryFolderRepo();
        var folder = await NewCreateFolderHandler(repo).HandleAsync(OrgId, "Stormboard", parentId: null);

        Assert.NotNull(folder);
        Assert.Equal(OrgId, folder!.OrganizationId);
        Assert.True(folder.IsTopLevel);
        Assert.Single(repo.Saved);
    }

    [Fact]
    public async Task CreateFolder_creates_a_child_under_an_existing_parent()
    {
        var repo = new InMemoryFolderRepo();
        var parent = Folder.CreateRoot(OrgId, "Stormboard");
        await repo.AddAsync(parent);

        var child = await NewCreateFolderHandler(repo).HandleAsync(OrgId, "Summarization", parent.Id);

        Assert.NotNull(child);
        Assert.Equal(parent.Id, child!.ParentId);
    }

    [Fact]
    public async Task CreateFolder_returns_null_when_the_organization_does_not_exist()
    {
        var repo = new InMemoryFolderRepo();

        var folder = await new CreateFolderHandler(new InMemoryOrganizationRepo(), repo)
            .HandleAsync(Guid.NewGuid(), "Orphan", parentId: null);

        Assert.Null(folder);
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task CreateFolder_returns_null_when_the_parent_does_not_exist()
    {
        var repo = new InMemoryFolderRepo();

        var folder = await NewCreateFolderHandler(repo).HandleAsync(OrgId, "Orphan", Guid.NewGuid());

        Assert.Null(folder);
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task CreateFolder_rejects_a_parent_in_a_different_organization()
    {
        var repo = new InMemoryFolderRepo();
        var otherOrgParent = Folder.CreateRoot(Guid.NewGuid(), "Elsewhere");
        await repo.AddAsync(otherOrgParent);

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewCreateFolderHandler(repo).HandleAsync(OrgId, "Child", otherOrgParent.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFolder_rejects_a_blank_name(string name)
    {
        var repo = new InMemoryFolderRepo();
        await Assert.ThrowsAsync<ArgumentException>(
            () => NewCreateFolderHandler(repo).HandleAsync(OrgId, name, null));
    }

    // ---- RenameFolderHandler ----

    [Fact]
    public async Task RenameFolder_changes_the_name()
    {
        var repo = new InMemoryFolderRepo();
        var folder = Folder.CreateRoot(OrgId, "Stormbaord");
        await repo.AddAsync(folder);

        var renamed = await new RenameFolderHandler(repo).HandleAsync(folder.Id, "Stormboard");

        Assert.Equal("Stormboard", renamed!.Name);
    }

    [Fact]
    public async Task RenameFolder_returns_null_when_the_folder_does_not_exist()
    {
        var repo = new InMemoryFolderRepo();
        Assert.Null(await new RenameFolderHandler(repo).HandleAsync(Guid.NewGuid(), "X"));
    }

    // ---- MoveFolderHandler ----

    [Fact]
    public async Task MoveFolder_reparents_under_another_folder()
    {
        var repo = new InMemoryFolderRepo();
        var a = Folder.CreateRoot(OrgId, "A");
        var b = Folder.CreateRoot(OrgId, "B");
        var child = Folder.CreateChild(OrgId, "Child", a.Id);
        await repo.AddAsync(a); await repo.AddAsync(b); await repo.AddAsync(child);

        var moved = await new MoveFolderHandler(repo).HandleAsync(child.Id, b.Id);

        Assert.Equal(b.Id, moved!.ParentId);
    }

    [Fact]
    public async Task MoveFolder_returns_null_when_the_folder_does_not_exist()
    {
        var repo = new InMemoryFolderRepo();
        Assert.Null(await new MoveFolderHandler(repo).HandleAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task MoveFolder_rejects_a_missing_target_parent()
    {
        var repo = new InMemoryFolderRepo();
        var folder = Folder.CreateRoot(OrgId, "A");
        await repo.AddAsync(folder);

        await Assert.ThrowsAsync<ArgumentException>(
            () => new MoveFolderHandler(repo).HandleAsync(folder.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task MoveFolder_rejects_moving_a_folder_under_its_own_descendant()
    {
        var repo = new InMemoryFolderRepo();
        var root = Folder.CreateRoot(OrgId, "Root");
        var child = Folder.CreateChild(OrgId, "Child", root.Id);
        var grandchild = Folder.CreateChild(OrgId, "Grandchild", child.Id);
        await repo.AddAsync(root); await repo.AddAsync(child); await repo.AddAsync(grandchild);

        // Moving root under its grandchild would form a cycle.
        await Assert.ThrowsAsync<ArgumentException>(
            () => new MoveFolderHandler(repo).HandleAsync(root.Id, grandchild.Id));
    }

    // ---- MovePromptHandler ----

    [Fact]
    public async Task MovePrompt_files_a_prompt_into_a_folder()
    {
        var folders = new InMemoryFolderRepo();
        var prompts = new InMemoryPromptRepo();
        var folder = Folder.CreateRoot(OrgId, "Stormboard");
        var prompt = Prompt.Create(OrgId, "Summarizer");
        await folders.AddAsync(folder);
        await prompts.AddAsync(prompt);

        var moved = await new MovePromptHandler(prompts, folders).HandleAsync(prompt.Id, folder.Id);

        Assert.Equal(folder.Id, moved!.FolderId);
    }

    [Fact]
    public async Task MovePrompt_unfiles_a_prompt_when_target_is_null()
    {
        var folders = new InMemoryFolderRepo();
        var prompts = new InMemoryPromptRepo();
        var folder = Folder.CreateRoot(OrgId, "Stormboard");
        var prompt = Prompt.Create(OrgId, "Summarizer", folderId: folder.Id);
        await folders.AddAsync(folder);
        await prompts.AddAsync(prompt);

        var moved = await new MovePromptHandler(prompts, folders).HandleAsync(prompt.Id, null);

        Assert.Null(moved!.FolderId);
    }

    [Fact]
    public async Task MovePrompt_returns_null_when_the_prompt_does_not_exist()
    {
        var folders = new InMemoryFolderRepo();
        var prompts = new InMemoryPromptRepo();

        Assert.Null(await new MovePromptHandler(prompts, folders).HandleAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task MovePrompt_rejects_a_missing_target_folder()
    {
        var folders = new InMemoryFolderRepo();
        var prompts = new InMemoryPromptRepo();
        var prompt = Prompt.Create(OrgId, "Summarizer");
        await prompts.AddAsync(prompt);

        await Assert.ThrowsAsync<ArgumentException>(
            () => new MovePromptHandler(prompts, folders).HandleAsync(prompt.Id, Guid.NewGuid()));
    }
}
