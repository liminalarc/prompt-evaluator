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

    [Fact]
    public async Task Weight_persists_default_and_explicit_and_survives_reweight()
    {
        var repo = new ScorerConfigRepository(_db);
        var datasetId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

        // One scorer with the default weight, one created with an explicit weight.
        var regex = ScorerConfig.Create(datasetId, ScorerDescriptor.Deterministic(ScorerKind.Regex, "^ok$"), at);
        var judge = ScorerConfig.Create(datasetId, ScorerDescriptor.LlmJudge("Rate quality", "claude-opus-4-8"), at, weight: 4.0);
        await repo.AddAsync(regex);
        await repo.AddAsync(judge);

        // Reweight the default one and persist the change.
        var loaded = await repo.GetByIdAsync(regex.Id);
        loaded!.SetWeight(2.5);
        await repo.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var configs = await repo.ListByDatasetAsync(datasetId);

        Assert.Equal(2.5, Assert.Single(configs, c => c.Scorer.Kind == ScorerKind.Regex).Weight);
        Assert.Equal(4.0, Assert.Single(configs, c => c.Scorer.Kind == ScorerKind.LlmJudge).Weight);
    }
}
