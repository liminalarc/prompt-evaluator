using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class VarianceAnalyticsTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryPromptRepo : IPromptRepository
    {
        private readonly List<Prompt> _saved = [];
        public Task AddAsync(Prompt prompt, CancellationToken ct = default) { _saved.Add(prompt); return Task.CompletedTask; }
        public Task<Prompt?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_saved.SingleOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Prompt>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Prompt>>(_saved);
        public Task<IReadOnlyList<Prompt>> ListByFolderAsync(Guid? folderId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Prompt>>(_saved.Where(p => p.FolderId == folderId).ToList());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { _saved.RemoveAll(p => p.Id == id); return Task.CompletedTask; }
    }

    private sealed class InMemoryEvalRunRepo : IEvalRunRepository
    {
        public readonly List<EvalRun> Saved = [];
        public Task AddAsync(EvalRun run, CancellationToken ct = default) { Saved.Add(run); return Task.CompletedTask; }
        public Task<EvalRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<EvalRun>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EvalRun>>(Saved.Where(r => r.DatasetId == datasetId).ToList());
        public Task<IReadOnlyList<EvalRun>> ListByPromptAndDatasetAsync(Guid promptId, Guid datasetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EvalRun>>(
                Saved.Where(r => r.PromptId == promptId && r.DatasetId == datasetId).ToList());
    }

    private static EvalRun RunFor(
        Guid promptId, Guid versionId, Guid datasetId, ScorerDescriptor scorer,
        DateTimeOffset at, IReadOnlyList<(Guid fixtureId, double value)> scores)
    {
        var run = EvalRun.Create(promptId, versionId, datasetId, at);
        foreach (var (fixtureId, value) in scores)
            run.RecordFixture(fixtureId, "out", 10, 0, 0, 0.001m).AddScore(scorer, value, value >= 0.5, null);
        return run;
    }

    [Fact]
    public async Task Variance_aggregates_all_runs_of_a_version_into_mean_and_spread()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Debrief");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-4-6", When, label: "baseline");
        var datasetId = Guid.NewGuid();
        var f0 = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch);

        var prompts = new InMemoryPromptRepo();
        await prompts.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        // Two runs of the SAME version — the wobble we want to see as spread, not two trend points.
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(f0, 0.9), (f1, 0.7)]));
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When.AddHours(1), [(f0, 0.8), (f1, 0.6)]));

        var handler = new VarianceAnalyticsHandler(runs, prompts);
        var result = await handler.HandleAsync(prompt.Id, datasetId);

        Assert.NotNull(result);
        var scorerVar = Assert.Single(result!);
        var version = Assert.Single(scorerVar.Versions);
        Assert.Equal(v1.Id, version.PromptVersionId);
        Assert.Equal(2, version.RunCount);

        // Aggregate = each run's per-fixture mean (0.8, 0.7) → mean 0.75, spread 0.05.
        Assert.Equal(0.75, version.Aggregate.Mean, 3);
        Assert.Equal(0.05, version.Aggregate.StdDev, 3);
        Assert.Equal(0.70, version.Aggregate.Min, 3);
        Assert.Equal(0.80, version.Aggregate.Max, 3);

        // Per-fixture spread across the two runs.
        var vf0 = Assert.Single(version.Fixtures, f => f.FixtureId == f0);
        Assert.Equal(0.85, vf0.Value.Mean, 3);
        Assert.Equal(0.05, vf0.Value.StdDev, 3);
        var vf1 = Assert.Single(version.Fixtures, f => f.FixtureId == f1);
        Assert.Equal(0.65, vf1.Value.Mean, 3);
        Assert.Equal(0.05, vf1.Value.StdDev, 3);
    }

    [Fact]
    public async Task Variance_is_graceful_for_a_single_run_n1_zero_spread()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Debrief");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-4-6", When);
        var datasetId = Guid.NewGuid();
        var f0 = Guid.NewGuid();
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch);

        var prompts = new InMemoryPromptRepo();
        await prompts.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(f0, 0.9)]));

        var result = await new VarianceAnalyticsHandler(runs, prompts).HandleAsync(prompt.Id, datasetId);

        var version = Assert.Single(Assert.Single(result!).Versions);
        Assert.Equal(1, version.RunCount);
        Assert.Equal(0.9, version.Aggregate.Mean, 3);
        Assert.Equal(0.0, version.Aggregate.StdDev, 3);
    }

    [Fact]
    public async Task Variance_returns_null_for_a_missing_prompt()
    {
        var result = await new VarianceAnalyticsHandler(new InMemoryEvalRunRepo(), new InMemoryPromptRepo())
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }
}
