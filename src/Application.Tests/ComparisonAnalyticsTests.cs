using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class ComparisonAnalyticsTests
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
            run.RecordFixture(fixtureId, "out", 10, 0.001m).AddScore(scorer, value, value >= 0.5, null);
        return run;
    }

    [Fact]
    public async Task Comparison_reports_per_fixture_and_aggregate_deltas()
    {
        var prompt = Prompt.Create("Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When, label: "baseline");
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1), label: "tweaked");
        var datasetId = Guid.NewGuid();
        var f0 = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch);

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(f0, 0.9), (f1, 0.5)]));
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddDays(1), [(f0, 0.7), (f1, 0.8)]));

        var handler = new ComparisonAnalyticsHandler(runs, promptRepo);
        var cmp = await handler.HandleAsync(prompt.Id, datasetId, v1.Id, v2.Id);

        Assert.NotNull(cmp);
        Assert.Equal(1, cmp!.FromVersionNumber);
        Assert.Equal(2, cmp.ToVersionNumber);
        Assert.Equal("baseline", cmp.FromVersionLabel);

        var sc = Assert.Single(cmp.Scorers);
        Assert.Equal(ScorerKind.FuzzyMatch, sc.Scorer.Kind);
        Assert.Equal(0.7, sc.FromMean!.Value, 3);   // (0.9 + 0.5)/2
        Assert.Equal(0.75, sc.ToMean!.Value, 3);    // (0.7 + 0.8)/2
        Assert.Equal(0.05, sc.Delta!.Value, 3);

        Assert.Equal(2, sc.Fixtures.Count);
        var d0 = Assert.Single(sc.Fixtures, x => x.FixtureId == f0);
        Assert.Equal(0.9, d0.FromValue!.Value, 3);
        Assert.Equal(0.7, d0.ToValue!.Value, 3);
        Assert.Equal(-0.2, d0.Delta!.Value, 3);
        var d1 = Assert.Single(sc.Fixtures, x => x.FixtureId == f1);
        Assert.Equal(0.3, d1.Delta!.Value, 3);
    }

    [Fact]
    public async Task Comparison_uses_the_latest_run_of_each_version()
    {
        var prompt = Prompt.Create("Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1));
        var datasetId = Guid.NewGuid();
        var f0 = Guid.NewGuid();
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch);

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(f0, 0.5)]));
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddHours(1), [(f0, 0.1)])); // stale
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddDays(1), [(f0, 0.9)]));  // latest

        var handler = new ComparisonAnalyticsHandler(runs, promptRepo);
        var cmp = await handler.HandleAsync(prompt.Id, datasetId, v1.Id, v2.Id);

        var sc = Assert.Single(cmp!.Scorers);
        Assert.Equal(0.9, sc.ToMean!.Value, 3);
        Assert.Equal(0.4, sc.Delta!.Value, 3);
    }

    [Fact]
    public async Task A_fixture_scored_on_only_one_side_has_a_null_delta()
    {
        var prompt = Prompt.Create("Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1));
        var datasetId = Guid.NewGuid();
        var shared = Guid.NewGuid();
        var onlyOld = Guid.NewGuid();
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch);

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(shared, 0.5), (onlyOld, 0.9)]));
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddDays(1), [(shared, 0.7)]));

        var handler = new ComparisonAnalyticsHandler(runs, promptRepo);
        var sc = Assert.Single((await handler.HandleAsync(prompt.Id, datasetId, v1.Id, v2.Id))!.Scorers);

        var removed = Assert.Single(sc.Fixtures, x => x.FixtureId == onlyOld);
        Assert.Equal(0.9, removed.FromValue!.Value, 3);
        Assert.Null(removed.ToValue);
        Assert.Null(removed.Delta);
    }

    [Fact]
    public async Task Returns_null_when_a_version_is_unknown()
    {
        var prompt = Prompt.Create("Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var handler = new ComparisonAnalyticsHandler(new InMemoryEvalRunRepo(), promptRepo);

        Assert.Null(await handler.HandleAsync(prompt.Id, Guid.NewGuid(), v1.Id, Guid.NewGuid()));
        Assert.Null(await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), v1.Id, v1.Id));
    }
}
