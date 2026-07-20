using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class VersionStatusTests
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

    private sealed class InMemoryDatasetRepo : IDatasetRepository
    {
        public readonly List<Dataset> Saved = [];
        public Task AddAsync(Dataset dataset, CancellationToken ct = default) { Saved.Add(dataset); return Task.CompletedTask; }
        public Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(d => d.Id == id));
        public Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Dataset>>(Saved);
        public Task<IReadOnlyList<Dataset>> ListByPromptAsync(Guid promptId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Dataset>>(Saved.Where(d => d.PromptId == promptId).ToList());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Saved.RemoveAll(d => d.Id == id); return Task.CompletedTask; }
    }

    // A run for `version` over `datasetId` scored by `scorer`; fixtures are keyed by the given ids so
    // consecutive versions can be paired for regression detection.
    private static EvalRun RunWith(
        Guid promptId, Guid versionId, Guid datasetId, ScorerDescriptor scorer, DateTimeOffset at,
        params (Guid fixtureId, double value, bool? passed)[] fixtures)
    {
        var run = EvalRun.Create(promptId, versionId, datasetId, at);
        foreach (var (fixtureId, value, passed) in fixtures)
        {
            var fr = run.RecordFixture(fixtureId, "out", latencyMs: 10, inputTokens: 0, outputTokens: 0, costUsd: 0.001m);
            fr.AddScore(scorer, value, passed, detail: null);
        }
        return run;
    }

    private static VersionStatusHandler Handler(InMemoryPromptRepo p, InMemoryEvalRunRepo r, InMemoryDatasetRepo d)
        => new(p, r, d, new RegressionDetector());

    [Fact]
    public async Task Unknown_prompt_returns_null()
    {
        var handler = Handler(new(), new(), new());
        Assert.Null(await handler.HandleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task With_no_current_set_nothing_is_current_or_eligible()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When.AddDays(1));
        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "DS"); await datasetRepo.AddAsync(ds);
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var f = Guid.NewGuid();
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, ds.Id, regex, When, (f, 0.5, true)));
        await runs.AddAsync(RunWith(prompt.Id, v2.Id, ds.Id, regex, When.AddDays(1), (f, 0.9, true)));

        var status = await Handler(promptRepo, runs, datasetRepo).HandleAsync(prompt.Id);

        Assert.NotNull(status);
        Assert.Null(status!.CurrentVersionId);
        Assert.All(status.Versions, v => Assert.False(v.IsCurrent));
        Assert.All(status.Versions, v => Assert.False(v.BackportEligible)); // nothing to compare against
    }

    [Fact]
    public async Task A_higher_scoring_version_above_current_is_backport_eligible()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When.AddDays(1));
        prompt.SetCurrentVersion(v1.Id, null, When); // shipped v1

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "DS"); await datasetRepo.AddAsync(ds);
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var f = Guid.NewGuid();
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, ds.Id, regex, When, (f, 0.5, true)));
        await runs.AddAsync(RunWith(prompt.Id, v2.Id, ds.Id, regex, When.AddDays(1), (f, 0.9, true)));

        var status = await Handler(promptRepo, runs, datasetRepo).HandleAsync(prompt.Id);

        var s1 = status!.Versions.Single(v => v.VersionId == v1.Id);
        var s2 = status.Versions.Single(v => v.VersionId == v2.Id);
        Assert.True(s1.IsCurrent);
        Assert.False(s1.BackportEligible); // Current is never its own backport
        Assert.True(s2.BackportEligible);  // v2 beats Current v1
        Assert.True(s2.IsBackportTarget);  // the only eligible → the target
        Assert.Equal(v2.Id, status.BackportTargetVersionId);
    }

    [Fact]
    public async Task Among_several_eligible_only_the_highest_scoring_is_the_backport_target()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When.AddDays(1));
        var v3 = prompt.AddVersion("v3", "claude-sonnet-5", When.AddDays(2));
        prompt.SetCurrentVersion(v1.Id, null, When); // shipped v1

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "DS"); await datasetRepo.AddAsync(ds);
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var f = Guid.NewGuid();
        // v2 and v3 both beat Current v1, but v3 scores higher → v3 is THE target, not v2.
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, ds.Id, regex, When, (f, 0.5, true)));
        await runs.AddAsync(RunWith(prompt.Id, v2.Id, ds.Id, regex, When.AddDays(1), (f, 0.7, true)));
        await runs.AddAsync(RunWith(prompt.Id, v3.Id, ds.Id, regex, When.AddDays(2), (f, 0.9, true)));

        var status = await Handler(promptRepo, runs, datasetRepo).HandleAsync(prompt.Id);

        // Both are eligible (the underlying signal), but only the highest-scoring is the single target.
        Assert.True(status!.Versions.Single(v => v.VersionId == v2.Id).BackportEligible);
        Assert.True(status.Versions.Single(v => v.VersionId == v3.Id).BackportEligible);
        Assert.False(status.Versions.Single(v => v.VersionId == v2.Id).IsBackportTarget);
        Assert.True(status.Versions.Single(v => v.VersionId == v3.Id).IsBackportTarget);
        Assert.Equal(v3.Id, status.BackportTargetVersionId);
        Assert.Single(status.Versions, v => v.IsBackportTarget); // exactly one
    }

    [Fact]
    public async Task A_version_that_loses_on_one_scorer_is_not_eligible()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When.AddDays(1));
        prompt.SetCurrentVersion(v1.Id, null, When);

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "DS"); await datasetRepo.AddAsync(ds);
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var judge = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");
        var f = Guid.NewGuid();
        // A single run per version carries every scorer (a real run scores each fixture with all
        // configured scorers). v2 beats v1 on regex but loses on the judge → not a clean backport.
        var v1Run = EvalRun.Create(prompt.Id, v1.Id, ds.Id, When);
        var v1Fr = v1Run.RecordFixture(f, "out", 10, 0, 0, 0.001m);
        v1Fr.AddScore(regex, 0.5, true, null);
        v1Fr.AddScore(judge, 0.9, null, null);
        await runs.AddAsync(v1Run);
        var v2Run = EvalRun.Create(prompt.Id, v2.Id, ds.Id, When.AddDays(1));
        var v2Fr = v2Run.RecordFixture(f, "out", 10, 0, 0, 0.001m);
        v2Fr.AddScore(regex, 0.9, true, null);
        v2Fr.AddScore(judge, 0.6, null, null);
        await runs.AddAsync(v2Run);

        var status = await Handler(promptRepo, runs, datasetRepo).HandleAsync(prompt.Id);

        Assert.False(status!.Versions.Single(v => v.VersionId == v2.Id).BackportEligible);
    }

    [Fact]
    public async Task Marking_the_top_version_current_clears_eligibility()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When.AddDays(1));
        prompt.SetCurrentVersion(v2.Id, null, When.AddDays(1)); // shipped the better one

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "DS"); await datasetRepo.AddAsync(ds);
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var f = Guid.NewGuid();
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, ds.Id, regex, When, (f, 0.5, true)));
        await runs.AddAsync(RunWith(prompt.Id, v2.Id, ds.Id, regex, When.AddDays(1), (f, 0.9, true)));

        var status = await Handler(promptRepo, runs, datasetRepo).HandleAsync(prompt.Id);

        Assert.All(status!.Versions, v => Assert.False(v.BackportEligible)); // nothing beats Current
        Assert.Equal(v2.Id, status.CurrentVersionId);
    }

    [Fact]
    public async Task A_version_that_dropped_vs_its_prior_is_flagged_regressed()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "P");
        var v1 = prompt.AddVersion("v1", "claude-sonnet-5", When);
        var v2 = prompt.AddVersion("v2", "claude-sonnet-5", When.AddDays(1));

        var promptRepo = new InMemoryPromptRepo(); await promptRepo.AddAsync(prompt);
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "DS"); await datasetRepo.AddAsync(ds);
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        // Two shared fixtures, a clear consistent drop v1→v2 (well past the 0.05 threshold).
        var fa = Guid.NewGuid(); var fb = Guid.NewGuid();
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, ds.Id, regex, When, (fa, 0.9, true), (fb, 0.9, true)));
        await runs.AddAsync(RunWith(prompt.Id, v2.Id, ds.Id, regex, When.AddDays(1), (fa, 0.3, false), (fb, 0.3, false)));

        var status = await Handler(promptRepo, runs, datasetRepo).HandleAsync(prompt.Id);

        Assert.True(status!.Versions.Single(v => v.VersionId == v2.Id).Regressed);
        Assert.False(status.Versions.Single(v => v.VersionId == v1.Id).Regressed);
    }
}
