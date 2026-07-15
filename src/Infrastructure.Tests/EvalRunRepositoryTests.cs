using Domain;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

public sealed class EvalRunRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .Build();

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
    public async Task Add_then_GetById_round_trips_the_run_with_fixtures_and_scores()
    {
        var repo = new EvalRunRepository(_db);
        var promptId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var fixtureId = Guid.NewGuid();

        var run = EvalRun.Create(promptId, versionId, datasetId,
            new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero));
        var fixture = run.RecordFixture(
            fixtureId, "the answer is 42", latencyMs: 512, inputTokens: 1000, outputTokens: 500, costUsd: 0.0034m);
        fixture.AddScore(ScorerDescriptor.Deterministic(ScorerKind.Regex, @"^\D*\d+"), value: 1.0, passed: true, detail: null);
        fixture.AddScore(ScorerDescriptor.LlmJudge("Is it correct?", "claude-opus-4-8"), value: 0.9, passed: null, detail: "accurate");

        await repo.AddAsync(run);
        _db.ChangeTracker.Clear();
        var loaded = await repo.GetByIdAsync(run.Id);

        Assert.NotNull(loaded);
        Assert.Equal(run.Id, loaded!.Id);
        Assert.Equal(promptId, loaded.PromptId);
        Assert.Equal(versionId, loaded.PromptVersionId);
        Assert.Equal(datasetId, loaded.DatasetId);

        var loadedFixture = Assert.Single(loaded.Results);
        Assert.Equal(fixtureId, loadedFixture.FixtureId);
        Assert.Equal("the answer is 42", loadedFixture.ModelOutput);
        Assert.Equal(512, loadedFixture.LatencyMs);
        Assert.Equal(1000, loadedFixture.InputTokens);
        Assert.Equal(500, loadedFixture.OutputTokens);
        Assert.Equal(0.0034m, loadedFixture.CostUsd);

        Assert.Equal(2, loadedFixture.Scores.Count);
        var judge = Assert.Single(loadedFixture.Scores, s => s.Scorer.Kind == ScorerKind.LlmJudge);
        Assert.Equal("claude-opus-4-8", judge.Scorer.JudgeModel);
        Assert.Equal(0.9, judge.Value);
        Assert.Equal("accurate", judge.Detail);
        var regex = Assert.Single(loadedFixture.Scores, s => s.Scorer.Kind == ScorerKind.Regex);
        Assert.True(regex.Passed);
        Assert.Null(regex.Scorer.JudgeModel);
    }

    [Fact]
    public async Task ListByPromptAndDataset_returns_only_that_prompts_runs_over_that_dataset()
    {
        var repo = new EvalRunRepository(_db);
        var promptId = Guid.NewGuid();
        var otherPromptId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var otherDatasetId = Guid.NewGuid();

        async Task Seed(Guid prompt, Guid dataset, DateTimeOffset at)
        {
            var run = EvalRun.Create(prompt, Guid.NewGuid(), dataset, at);
            run.RecordFixture(Guid.NewGuid(), "x", 1, 0, 0, 0.0m)
                .AddScore(ScorerDescriptor.Deterministic(ScorerKind.Regex, "^x"), 1.0, true, null);
            await repo.AddAsync(run);
        }

        var t0 = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
        await Seed(promptId, datasetId, t0);
        await Seed(promptId, datasetId, t0.AddHours(2));      // newer → first
        await Seed(promptId, otherDatasetId, t0);            // wrong dataset
        await Seed(otherPromptId, datasetId, t0);            // wrong prompt
        _db.ChangeTracker.Clear();

        var result = await repo.ListByPromptAndDatasetAsync(promptId, datasetId);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(promptId, r.PromptId));
        Assert.All(result, r => Assert.Equal(datasetId, r.DatasetId));
        Assert.True(result[0].CreatedAt >= result[1].CreatedAt); // newest first
        Assert.Single(result[0].Results); // fixture + score graph loaded eagerly
    }

    [Fact]
    public async Task SystemInfo_reports_the_postgres_engine_version()
    {
        var systemInfo = new SystemInfo(_db);

        var version = await systemInfo.GetDatabaseVersionAsync();

        Assert.StartsWith("PostgreSQL", version);
    }
}
