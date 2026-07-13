using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

public sealed class DatasetRepositoryTests : IAsyncLifetime
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

    // A dataset belongs to a prompt (1.7) and the FK enforces it, so every dataset test seeds an
    // owning prompt first.
    private async Task<Guid> SeedPromptAsync()
    {
        var prompt = Prompt.Create("Owner");
        await using var ctx = NewContext();
        await new PromptRepository(ctx).AddAsync(prompt);
        return prompt.Id;
    }

    [Fact]
    public async Task Add_then_GetById_round_trips_fixtures_with_origin_and_seed_link()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var promptId = await SeedPromptAsync();
        var dataset = Dataset.Create(promptId, "Summaries", "captured SLM output");
        var seed = dataset.AddCapturedFixture(
            "summarize this", When, upstreamContext: "raw slm output", expectedOutput: "the summary");
        dataset.AddSyntheticFixture(
            "summarize that", seed.Id, When.AddMinutes(1), upstreamContext: "slm-shaped output");

        await using (var writeCtx = NewContext())
        {
            await new DatasetRepository(writeCtx).AddAsync(dataset);
        }

        await using var readCtx = NewContext();
        var loaded = await new DatasetRepository(readCtx).GetByIdAsync(dataset.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Summaries", loaded!.Name);
        Assert.Equal(2, loaded.Fixtures.Count);

        var captured = Assert.Single(loaded.Fixtures, f => f.Origin == FixtureOrigin.Captured);
        Assert.Equal("summarize this", captured.Input);
        Assert.Equal("raw slm output", captured.UpstreamContext);
        Assert.Equal("the summary", captured.ExpectedOutput);
        Assert.Null(captured.SeedFixtureId);

        var synthetic = Assert.Single(loaded.Fixtures, f => f.Origin == FixtureOrigin.Synthetic);
        Assert.Equal(seed.Id, synthetic.SeedFixtureId);
    }

    [Fact]
    public async Task AddFixtures_to_a_tracked_dataset_then_SaveChanges_persists_them()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var promptId = await SeedPromptAsync();
        var dataset = Dataset.Create(promptId, "Classifications");
        dataset.AddCapturedFixture("classify a", When);

        await using (var writeCtx = NewContext())
        {
            await new DatasetRepository(writeCtx).AddAsync(dataset);
        }

        await using (var editCtx = NewContext())
        {
            var repo = new DatasetRepository(editCtx);
            var loaded = await repo.GetByIdAsync(dataset.Id);
            loaded!.AddCapturedFixture("classify b", When.AddMinutes(5));
            await repo.SaveChangesAsync();
        }

        await using var readCtx = NewContext();
        var reloaded = await new DatasetRepository(readCtx).GetByIdAsync(dataset.Id);

        Assert.Equal(2, reloaded!.Fixtures.Count);
    }

    [Fact]
    public async Task List_returns_all_datasets_with_their_fixtures()
    {
        await using var migrateCtx = NewContext();
        await migrateCtx.Database.MigrateAsync();

        var promptId = await SeedPromptAsync();
        var a = Dataset.Create(promptId, "A");
        a.AddCapturedFixture("a1", When);
        var b = Dataset.Create(promptId, "B");
        b.AddCapturedFixture("b1", When);

        await using (var writeCtx = NewContext())
        {
            var repo = new DatasetRepository(writeCtx);
            await repo.AddAsync(a);
            await repo.AddAsync(b);
        }

        await using var readCtx = NewContext();
        var all = await new DatasetRepository(readCtx).ListAsync();

        Assert.Equal(2, all.Count);
        Assert.All(all, d => Assert.Single(d.Fixtures));
    }
}
