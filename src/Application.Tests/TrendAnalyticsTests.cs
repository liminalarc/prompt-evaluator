using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class TrendAnalyticsTests
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

    // A run over `datasetId` for `version`, one fixture per (value, passed) pair, all scored by `scorer`.
    private static EvalRun RunWith(
        Guid promptId, Guid versionId, Guid datasetId, ScorerDescriptor scorer,
        DateTimeOffset at, params (double value, bool? passed)[] fixtureScores)
    {
        var run = EvalRun.Create(promptId, versionId, datasetId, at);
        foreach (var (value, passed) in fixtureScores)
        {
            var fr = run.RecordFixture(Guid.NewGuid(), "out", latencyMs: 10, inputTokens: 0, outputTokens: 0, costUsd: 0.001m);
            fr.AddScore(scorer, value, passed, detail: null);
        }
        return run;
    }

    private static (Prompt prompt, PromptVersion v1, PromptVersion v2) TwoVersionPrompt()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Summarizer");
        var v1 = prompt.AddVersion("v1 content", "claude-opus-4-8", When, label: "baseline");
        var v2 = prompt.AddVersion("v2 content", "claude-opus-4-8", When.AddDays(1), label: "tweaked");
        return (prompt, v1, v2);
    }

    [Fact]
    public async Task Trend_series_has_one_point_per_version_ordered_by_version_number()
    {
        var (prompt, v1, v2) = TwoVersionPrompt();
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        // Seed v2 first to prove ordering is by VersionNumber, not insertion/time.
        await runs.AddAsync(RunWith(prompt.Id, v2.Id, datasetId, regex, When.AddDays(1), (0.6, true), (0.8, true)));
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, datasetId, regex, When, (0.4, false), (0.6, true)));

        var handler = new TrendAnalyticsHandler(runs, promptRepo);
        var series = await handler.HandleAsync(prompt.Id, datasetId);

        Assert.NotNull(series);
        var s = Assert.Single(series!); // one scorer identity
        Assert.Equal(regex.Identity, s.Scorer.Identity);
        Assert.Equal(ScorerKind.Regex, s.Scorer.Kind);

        Assert.Equal(2, s.Points.Count);
        Assert.Equal([1, 2], s.Points.Select(p => p.VersionNumber).ToArray()); // ascending
        Assert.Equal("baseline", s.Points[0].VersionLabel);
        Assert.Equal(0.5, s.Points[0].MeanValue, 3);   // (0.4 + 0.6)/2
        Assert.Equal(0.7, s.Points[1].MeanValue, 3);   // (0.6 + 0.8)/2
        Assert.Equal(2, s.Points[0].FixtureCount);
    }

    [Fact]
    public async Task Trend_point_uses_the_latest_run_of_a_version()
    {
        var (prompt, v1, _) = TwoVersionPrompt();
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        // Two runs of v1; the newer (later CreatedAt) must win even though seeded first.
        var newer = RunWith(prompt.Id, v1.Id, datasetId, regex, When.AddHours(5), (0.9, true));
        var older = RunWith(prompt.Id, v1.Id, datasetId, regex, When, (0.1, false));
        await runs.AddAsync(newer);
        await runs.AddAsync(older);

        var handler = new TrendAnalyticsHandler(runs, promptRepo);
        var series = await handler.HandleAsync(prompt.Id, datasetId);

        var point = Assert.Single(Assert.Single(series!).Points);
        Assert.Equal(newer.Id, point.RunId);
        Assert.Equal(0.9, point.MeanValue, 3);
    }

    [Fact]
    public async Task Distinct_scorers_form_distinct_series()
    {
        var (prompt, v1, _) = TwoVersionPrompt();
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var judge = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        var run = EvalRun.Create(prompt.Id, v1.Id, datasetId, When);
        var fr = run.RecordFixture(Guid.NewGuid(), "out", 10, 0, 0, 0.001m);
        fr.AddScore(regex, 1.0, true, null);
        fr.AddScore(judge, 0.8, null, "ok");
        await runs.AddAsync(run);

        var handler = new TrendAnalyticsHandler(runs, promptRepo);
        var series = await handler.HandleAsync(prompt.Id, datasetId);

        Assert.Equal(2, series!.Count);
        Assert.Contains(series, x => x.Scorer.Kind == ScorerKind.Regex);
        var judgeSeries = Assert.Single(series, x => x.Scorer.Kind == ScorerKind.LlmJudge);
        Assert.Equal("claude-opus-4-8", judgeSeries.Scorer.JudgeModel);
        Assert.Equal(0.8, Assert.Single(judgeSeries.Points).MeanValue, 3);
    }

    [Fact]
    public async Task Pass_rate_is_the_fraction_passed_among_scores_with_a_verdict()
    {
        var (prompt, v1, _) = TwoVersionPrompt();
        var datasetId = Guid.NewGuid();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        // 3 fixtures: passed, failed, no-verdict → pass rate 1/2 over the two with a verdict.
        await runs.AddAsync(RunWith(prompt.Id, v1.Id, datasetId, regex, When, (1.0, true), (0.0, false), (0.5, null)));

        var handler = new TrendAnalyticsHandler(runs, promptRepo);
        var point = Assert.Single(Assert.Single((await handler.HandleAsync(prompt.Id, datasetId))!).Points);

        Assert.Equal(0.5, point.PassRate);
        Assert.Equal(3, point.FixtureCount);
    }

    [Fact]
    public async Task Unknown_prompt_returns_null()
    {
        var handler = new TrendAnalyticsHandler(new InMemoryEvalRunRepo(), new InMemoryPromptRepo());
        Assert.Null(await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task No_runs_yields_empty_series_list()
    {
        var (prompt, _, _) = TwoVersionPrompt();
        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var handler = new TrendAnalyticsHandler(new InMemoryEvalRunRepo(), promptRepo);

        var series = await handler.HandleAsync(prompt.Id, Guid.NewGuid());
        Assert.NotNull(series);
        Assert.Empty(series!);
    }
}
