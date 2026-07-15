using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

/// <summary>
/// Round-trips the 1.10 delete-with-cascade / reparent behaviour against a real Postgres: a prompt
/// takes its versions, datasets, fixtures, scorer-configs, eval-runs and scores with it; a dataset
/// takes its own; and a folder reparents its children/prompts before it goes (never deleting them).
/// </summary>
public sealed class DeletionRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset When = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

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

    // Seeds a prompt (with a version) that owns a dataset (with fixtures), plus a scorer-config and
    // an eval-run (with a fixture-run + scores) scoped to that dataset. Returns the ids to assert on.
    private async Task<(Guid PromptId, Guid VersionId, Guid DatasetId, Guid ScorerConfigId, Guid EvalRunId)>
        SeedGraphAsync(Guid orgId, Guid? folderId = null)
    {
        var prompt = Prompt.Create(orgId, "Summarizer", folderId: folderId);
        prompt.AddVersion("Summarize: {input}", "claude-sonnet-5", When);
        var versionId = prompt.Versions[0].Id;

        var dataset = Dataset.Create(prompt.Id, "Summaries");
        var fixture = dataset.AddCapturedFixture("summarize this", When);

        var scorer = ScorerConfig.Create(
            dataset.Id, ScorerDescriptor.Deterministic(ScorerKind.Regex, "^ok$"), When);

        var run = EvalRun.Create(prompt.Id, versionId, dataset.Id, When);
        run.RecordFixture(fixture.Id, "the answer", latencyMs: 10, inputTokens: 1, outputTokens: 1, costUsd: 0m)
            .AddScore(ScorerDescriptor.Deterministic(ScorerKind.Regex, "^the"), 1.0, true, null);

        await using var ctx = NewContext();
        await new PromptRepository(ctx).AddAsync(prompt);
        await new DatasetRepository(ctx).AddAsync(dataset);
        await new ScorerConfigRepository(ctx).AddAsync(scorer);
        await new EvalRunRepository(ctx).AddAsync(run);

        return (prompt.Id, versionId, dataset.Id, scorer.Id, run.Id);
    }

    private async Task<Guid> SeedOrgAsync()
    {
        var org = Organization.Create("Acme");
        await using var ctx = NewContext();
        await new OrganizationRepository(ctx).AddAsync(org);
        return org.Id;
    }

    [Fact]
    public async Task Deleting_a_prompt_cascades_its_datasets_scorers_runs_and_scores()
    {
        await MigrateAsync();
        var orgId = await SeedOrgAsync();
        var graph = await SeedGraphAsync(orgId);
        // A sibling prompt with its own graph must be left untouched.
        var sibling = await SeedGraphAsync(orgId);

        await using (var del = NewContext())
        {
            await new PromptRepository(del).DeleteAsync(graph.PromptId);
        }

        await using var read = NewContext();
        Assert.Null(await new PromptRepository(read).GetByIdAsync(graph.PromptId));
        Assert.Empty(await read.Datasets.Where(d => d.Id == graph.DatasetId).ToListAsync());
        Assert.Empty(await read.ScorerConfigs.Where(c => c.DatasetId == graph.DatasetId).ToListAsync());
        Assert.Empty(await read.EvalRuns.Where(r => r.PromptId == graph.PromptId).ToListAsync());
        // Owned rows under the run cascade at the DB level too.
        Assert.Empty(await read.Set<Domain.EvalRun>().Where(r => r.Id == graph.EvalRunId).ToListAsync());

        // The sibling survives in full.
        Assert.NotNull(await new PromptRepository(read).GetByIdAsync(sibling.PromptId));
        Assert.Single(await read.Datasets.Where(d => d.Id == sibling.DatasetId).ToListAsync());
        Assert.Single(await read.ScorerConfigs.Where(c => c.DatasetId == sibling.DatasetId).ToListAsync());
        Assert.Single(await read.EvalRuns.Where(r => r.Id == sibling.EvalRunId).ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_dataset_cascades_its_fixtures_scorers_runs_and_scores()
    {
        await MigrateAsync();
        var orgId = await SeedOrgAsync();
        var graph = await SeedGraphAsync(orgId);

        await using (var del = NewContext())
        {
            await new DatasetRepository(del).DeleteAsync(graph.DatasetId);
        }

        await using var read = NewContext();
        Assert.Null(await new DatasetRepository(read).GetByIdAsync(graph.DatasetId));
        Assert.Empty(await read.ScorerConfigs.Where(c => c.DatasetId == graph.DatasetId).ToListAsync());
        Assert.Empty(await read.EvalRuns.Where(r => r.DatasetId == graph.DatasetId).ToListAsync());

        // The owning prompt (and its version) is untouched — only the dataset was deleted.
        Assert.NotNull(await new PromptRepository(read).GetByIdAsync(graph.PromptId));
    }

    [Fact]
    public async Task Deleting_a_folder_reparents_its_children_and_prompts_then_removes_it()
    {
        await MigrateAsync();
        var orgId = await SeedOrgAsync();

        var root = Folder.CreateRoot(orgId, "Root");
        var middle = Folder.CreateChild(orgId, "Middle", root.Id);
        var leaf = Folder.CreateChild(orgId, "Leaf", middle.Id);
        var filed = Prompt.Create(orgId, "Filed", folderId: middle.Id);

        await using (var write = NewContext())
        {
            var folders = new FolderRepository(write);
            await folders.AddAsync(root);
            await folders.AddAsync(middle);
            await folders.AddAsync(leaf);
            await new PromptRepository(write).AddAsync(filed);
        }

        await using (var del = NewContext())
        {
            await new FolderRepository(del).DeleteAsync(middle.Id);
        }

        await using var read = NewContext();
        // The folder is gone…
        Assert.Null(await new FolderRepository(read).GetByIdAsync(middle.Id));
        // …its child folder is reparented to the deleted folder's parent (root), not deleted…
        var reloadedLeaf = await new FolderRepository(read).GetByIdAsync(leaf.Id);
        Assert.NotNull(reloadedLeaf);
        Assert.Equal(root.Id, reloadedLeaf!.ParentId);
        // …and its prompt moves up with it (not deleted).
        var reloadedPrompt = await new PromptRepository(read).GetByIdAsync(filed.Id);
        Assert.NotNull(reloadedPrompt);
        Assert.Equal(root.Id, reloadedPrompt!.FolderId);
    }

    [Fact]
    public async Task Deleting_a_top_level_folder_promotes_children_to_the_org_root()
    {
        await MigrateAsync();
        var orgId = await SeedOrgAsync();

        var root = Folder.CreateRoot(orgId, "Root");
        var child = Folder.CreateChild(orgId, "Child", root.Id);
        var filed = Prompt.Create(orgId, "Filed", folderId: root.Id);

        await using (var write = NewContext())
        {
            var folders = new FolderRepository(write);
            await folders.AddAsync(root);
            await folders.AddAsync(child);
            await new PromptRepository(write).AddAsync(filed);
        }

        await using (var del = NewContext())
        {
            await new FolderRepository(del).DeleteAsync(root.Id);
        }

        await using var read = NewContext();
        Assert.Null(await new FolderRepository(read).GetByIdAsync(root.Id));
        // Top-level folder had no parent, so its contents drop to the org root (null).
        var reloadedChild = await new FolderRepository(read).GetByIdAsync(child.Id);
        Assert.True(reloadedChild!.IsTopLevel);
        var reloadedPrompt = await new PromptRepository(read).GetByIdAsync(filed.Id);
        Assert.Null(reloadedPrompt!.FolderId);
    }

    [Fact]
    public async Task Deleting_a_missing_entity_is_a_no_op()
    {
        await MigrateAsync();

        await using var ctx = NewContext();
        // None of these should throw for an unknown id.
        await new PromptRepository(ctx).DeleteAsync(Guid.NewGuid());
        await new DatasetRepository(ctx).DeleteAsync(Guid.NewGuid());
        await new FolderRepository(ctx).DeleteAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task Deleting_an_org_also_removes_its_orphan_eval_runs_and_scorer_configs()
    {
        await MigrateAsync();
        var orgId = await SeedOrgAsync();
        var graph = await SeedGraphAsync(orgId);

        await using (var del = NewContext())
        {
            await new OrganizationRepository(del).DeleteAsync(orgId);
        }

        await using var read = NewContext();
        // A bare org FK-cascade would orphan these FK-less analytics rows — assert they're gone.
        Assert.Empty(await read.EvalRuns.Where(r => r.Id == graph.EvalRunId).ToListAsync());
        Assert.Empty(await read.ScorerConfigs.Where(c => c.Id == graph.ScorerConfigId).ToListAsync());
        // …and the org's prompt/dataset cascade away as before.
        Assert.Empty(await read.Prompts.Where(p => p.Id == graph.PromptId).ToListAsync());
        Assert.Empty(await read.Datasets.Where(d => d.Id == graph.DatasetId).ToListAsync());
    }
}
