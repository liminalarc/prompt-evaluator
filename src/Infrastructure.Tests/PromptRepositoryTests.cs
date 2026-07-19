using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

public sealed class PromptRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

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

    // A prompt belongs to an organization (1.9) and the FK enforces it, so seed one first.
    private async Task<Guid> SeedOrgAsync()
    {
        var org = Organization.Create("Acme");
        await using var ctx = NewContext();
        await new OrganizationRepository(ctx).AddAsync(org);
        return org.Id;
    }

    [Fact]
    public async Task Add_then_GetById_round_trips_the_full_ordered_version_history()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var orgId = await SeedOrgAsync();
        var prompt = Prompt.Create(orgId, "Summarizer", "Summarizes captured SLM output");
        prompt.AddVersion("Summarize: {input}", "claude-sonnet-5", When, label: "baseline", sourceApp: "Stormboard");
        prompt.AddVersion("Summarize concisely: {input}", "claude-opus-4-8", When.AddMinutes(1));

        await using (var writeCtx = NewContext())
        {
            await new PromptRepository(writeCtx).AddAsync(prompt);
        }

        await using var readCtx = NewContext();
        var loaded = await new PromptRepository(readCtx).GetByIdAsync(prompt.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Summarizer", loaded!.Name);
        Assert.Equal("Summarizes captured SLM output", loaded.Description);
        Assert.Equal(new[] { 1, 2 }, loaded.Versions.Select(v => v.VersionNumber));

        var v1 = loaded.Versions[0];
        Assert.Equal("Summarize: {input}", v1.Content);
        Assert.Equal("claude-sonnet-5", v1.TargetModel);
        Assert.Equal("baseline", v1.Label);
        Assert.Equal("Stormboard", v1.SourceApp);

        var v2 = loaded.Versions[1];
        Assert.Equal("claude-opus-4-8", v2.TargetModel);
        Assert.Null(v2.Label);
        Assert.Null(v2.SourceApp);
    }

    [Fact]
    public async Task AddVersion_to_a_tracked_prompt_then_SaveChanges_persists_the_new_version()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var orgId = await SeedOrgAsync();
        var prompt = Prompt.Create(orgId, "Classifier");
        prompt.AddVersion("Classify: {input}", "claude-sonnet-5", When);

        await using (var writeCtx = NewContext())
        {
            await new PromptRepository(writeCtx).AddAsync(prompt);
        }

        await using (var editCtx = NewContext())
        {
            var repo = new PromptRepository(editCtx);
            var loaded = await repo.GetByIdAsync(prompt.Id);
            loaded!.AddVersion("Classify precisely: {input}", "claude-opus-4-8", When.AddMinutes(5));
            await repo.SaveChangesAsync();
        }

        await using var readCtx = NewContext();
        var reloaded = await new PromptRepository(readCtx).GetByIdAsync(prompt.Id);

        Assert.Equal(new[] { 1, 2 }, reloaded!.Versions.Select(v => v.VersionNumber));
        Assert.Equal("Classify precisely: {input}", reloaded.Versions[1].Content);
    }

    [Fact]
    public async Task SetCurrentVersion_round_trips_the_marker_sha_and_timestamp()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var orgId = await SeedOrgAsync();
        var prompt = Prompt.Create(orgId, "Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        prompt.AddVersion("v2", "claude-sonnet-5", When.AddMinutes(1));

        await using (var writeCtx = NewContext())
        {
            await new PromptRepository(writeCtx).AddAsync(prompt);
        }

        await using (var editCtx = NewContext())
        {
            var repo = new PromptRepository(editCtx);
            var loaded = await repo.GetByIdAsync(prompt.Id);
            Assert.Null(loaded!.CurrentVersionId); // nullable until first set
            loaded.SetCurrentVersion(v1.Id, "deadbeef", When.AddDays(1));
            await repo.SaveChangesAsync();
        }

        await using var readCtx = NewContext();
        var reloaded = await new PromptRepository(readCtx).GetByIdAsync(prompt.Id);

        Assert.Equal(v1.Id, reloaded!.CurrentVersionId);
        Assert.Equal("deadbeef", reloaded.CurrentVersionSha);
        Assert.Equal(When.AddDays(1), reloaded.CurrentVersionSetAt);
    }

    [Fact]
    public async Task List_returns_all_prompts_with_their_versions()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var orgId = await SeedOrgAsync();
        var a = Prompt.Create(orgId, "A");
        a.AddVersion("a1", "claude-sonnet-5", When);
        var b = Prompt.Create(orgId, "B");
        b.AddVersion("b1", "claude-sonnet-5", When);

        await using (var writeCtx = NewContext())
        {
            var repo = new PromptRepository(writeCtx);
            await repo.AddAsync(a);
            await repo.AddAsync(b);
        }

        await using var readCtx = NewContext();
        var all = await new PromptRepository(readCtx).ListAsync();

        Assert.Equal(2, all.Count);
        Assert.All(all, p => Assert.Single(p.Versions));
    }
}
