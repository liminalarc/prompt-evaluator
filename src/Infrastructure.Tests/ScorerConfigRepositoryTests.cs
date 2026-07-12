using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

public sealed class ScorerConfigRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private EvalDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<EvalDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new EvalDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Add_then_ListByDataset_round_trips_scorer_identity()
    {
        var repo = new ScorerConfigRepository(_db);
        var datasetId = Guid.NewGuid();
        var other = Guid.NewGuid();

        await repo.AddAsync(ScorerConfig.Create(datasetId,
            ScorerDescriptor.Deterministic(ScorerKind.Regex, "^ok$"), new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero)));
        await repo.AddAsync(ScorerConfig.Create(datasetId,
            ScorerDescriptor.LlmJudge("Rate quality", "claude-opus-4-8"), new DateTimeOffset(2026, 7, 12, 1, 0, 0, TimeSpan.Zero)));
        await repo.AddAsync(ScorerConfig.Create(other,
            ScorerDescriptor.Deterministic(ScorerKind.ExactMatch), new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero)));

        _db.ChangeTracker.Clear();
        var configs = await repo.ListByDatasetAsync(datasetId);

        Assert.Equal(2, configs.Count); // scoped to the dataset
        var judge = Assert.Single(configs, c => c.Scorer.Kind == ScorerKind.LlmJudge);
        Assert.Equal("claude-opus-4-8", judge.Scorer.JudgeModel);
        Assert.Equal("Rate quality", judge.Scorer.Config);
        var regex = Assert.Single(configs, c => c.Scorer.Kind == ScorerKind.Regex);
        Assert.Equal("^ok$", regex.Scorer.Config);
        Assert.Null(regex.Scorer.JudgeModel);
    }
}
