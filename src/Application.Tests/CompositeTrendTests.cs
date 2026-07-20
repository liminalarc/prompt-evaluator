using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class CompositeTrendTests
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
            => Task.FromResult<IReadOnlyList<EvalRun>>(Saved.Where(r => r.PromptId == promptId && r.DatasetId == datasetId).ToList());
    }

    private sealed class InMemoryScorerConfigRepo : IScorerConfigRepository
    {
        public readonly List<ScorerConfig> Saved = [];
        public Task AddAsync(ScorerConfig config, CancellationToken ct = default) { Saved.Add(config); return Task.CompletedTask; }
        public Task<IReadOnlyList<ScorerConfig>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ScorerConfig>>(Saved.Where(c => c.DatasetId == datasetId).ToList());
        public Task<ScorerConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(c => c.Id == id));
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(Guid id, CancellationToken ct = default) { Saved.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Composite_blends_the_scorers_by_their_configured_weight()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When, label: "baseline");
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var judge = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        var run = EvalRun.Create(prompt.Id, v1.Id, datasetId, When);
        var fr = run.RecordFixture(Guid.NewGuid(), "out", 10, 0, 0, 0.001m);
        fr.AddScore(regex, 0.4, true, null);
        fr.AddScore(judge, 0.9, null, "ok");
        await runs.AddAsync(run);

        var scorerConfigs = new InMemoryScorerConfigRepo();
        scorerConfigs.Saved.Add(ScorerConfig.Create(datasetId, regex, When, weight: 1.0));
        scorerConfigs.Saved.Add(ScorerConfig.Create(datasetId, judge, When, weight: 4.0));

        var handler = new CompositeTrendHandler(runs, promptRepo, scorerConfigs);
        var points = await handler.HandleAsync(prompt.Id, datasetId);

        var p = Assert.Single(points!);
        Assert.Equal(1, p.VersionNumber);
        Assert.Equal("baseline", p.VersionLabel);
        Assert.Equal(2, p.ScorerCount);
        Assert.Equal(0.8, p.CompositeValue, 9); // (1*0.4 + 4*0.9)/5
    }

    [Fact]
    public async Task Composite_renormalizes_when_the_scorer_set_changes_between_versions()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1));
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var judge = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();

        // v1 scored by judge only → composite = the judge mean (renormalized over its weight alone).
        var r1 = EvalRun.Create(prompt.Id, v1.Id, datasetId, When);
        r1.RecordFixture(Guid.NewGuid(), "out", 10, 0, 0, 0.001m).AddScore(judge, 0.8, null, null);
        await runs.AddAsync(r1);

        // v2 scored by both → (4*0.8 + 1*0.4)/5 = 0.72.
        var r2 = EvalRun.Create(prompt.Id, v2.Id, datasetId, When.AddDays(1));
        var fr2 = r2.RecordFixture(Guid.NewGuid(), "out", 10, 0, 0, 0.001m);
        fr2.AddScore(judge, 0.8, null, null);
        fr2.AddScore(regex, 0.4, true, null);
        await runs.AddAsync(r2);

        var scorerConfigs = new InMemoryScorerConfigRepo();
        scorerConfigs.Saved.Add(ScorerConfig.Create(datasetId, judge, When, weight: 4.0));
        scorerConfigs.Saved.Add(ScorerConfig.Create(datasetId, regex, When, weight: 1.0));

        var handler = new CompositeTrendHandler(runs, promptRepo, scorerConfigs);
        var points = (await handler.HandleAsync(prompt.Id, datasetId))!;

        Assert.Equal([1, 2], points.Select(p => p.VersionNumber).ToArray());
        Assert.Equal(0.8, points[0].CompositeValue, 9);  // judge only
        Assert.Equal(1, points[0].ScorerCount);
        Assert.Equal(0.72, points[1].CompositeValue, 9); // both, renormalized
        Assert.Equal(2, points[1].ScorerCount);
    }

    [Fact]
    public async Task A_run_scorer_with_no_current_weight_falls_back_to_one()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var judge = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        var run = EvalRun.Create(prompt.Id, v1.Id, datasetId, When);
        var fr = run.RecordFixture(Guid.NewGuid(), "out", 10, 0, 0, 0.001m);
        fr.AddScore(regex, 0.4, true, null);
        fr.AddScore(judge, 1.0, null, null);
        await runs.AddAsync(run);

        // Only the regex weight is configured; the judge run-score has no current row → weight 1.0.
        var scorerConfigs = new InMemoryScorerConfigRepo();
        scorerConfigs.Saved.Add(ScorerConfig.Create(datasetId, regex, When, weight: 3.0));

        var handler = new CompositeTrendHandler(runs, promptRepo, scorerConfigs);
        var p = Assert.Single((await handler.HandleAsync(prompt.Id, datasetId))!);

        Assert.Equal(0.55, p.CompositeValue, 9); // (3*0.4 + 1*1.0)/4
    }

    [Fact]
    public async Task Unknown_prompt_returns_null()
    {
        var handler = new CompositeTrendHandler(new InMemoryEvalRunRepo(), new InMemoryPromptRepo(), new InMemoryScorerConfigRepo());
        Assert.Null(await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
