using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class RegressionDetectionTests
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

    private static readonly ScorerRef Scorer =
        ScorerRef.From(ScorerDescriptor.Deterministic(ScorerKind.Regex, "^x"));

    // A version's per-fixture scores for one scorer. Fixtures are named f1..fn for matching.
    private static VersionScoreSet Version(int number, params double[] values)
    {
        var byFixture = new Dictionary<Guid, double>();
        for (var i = 0; i < values.Length; i++)
            byFixture[FixtureId(i)] = values[i];
        return new VersionScoreSet(Guid.NewGuid(), number, $"v{number}", Guid.NewGuid(), byFixture);
    }

    // Stable per-index fixture id so matched fixtures line up across versions.
    private static Guid FixtureId(int i) => new($"00000000-0000-0000-0000-{i:D12}");

    private readonly RegressionDetector _detector = new();

    [Fact]
    public void Consistent_drop_beyond_threshold_is_flagged_as_significant()
    {
        var flags = _detector.Detect(Scorer,
            [Version(1, 0.9, 0.9, 0.9, 0.9), Version(2, 0.7, 0.7, 0.7, 0.7)]);

        var flag = Assert.Single(flags);
        Assert.Equal(1, flag.FromVersionNumber);
        Assert.Equal(2, flag.ToVersionNumber);
        Assert.Equal(0.9, flag.PriorMean, 3);
        Assert.Equal(0.7, flag.CurrentMean, 3);
        Assert.Equal(-0.2, flag.Delta, 3);
        Assert.Equal(4, flag.PairedFixtureCount);
        Assert.NotNull(flag.PValue);
        Assert.True(flag.PValue < 0.05, $"expected significant, p={flag.PValue}");
        Assert.Equal(RegressionConfidence.Confirmed, flag.Confidence);
    }

    [Fact]
    public void Drop_within_threshold_is_not_flagged()
    {
        var flags = _detector.Detect(Scorer,
            [Version(1, 0.90, 0.90), Version(2, 0.88, 0.88)]); // 0.02 drop < 0.05

        Assert.Empty(flags);
    }

    [Fact]
    public void Large_but_noisy_drop_beyond_threshold_is_flagged_as_unverified()
    {
        // Means drop 0.8 -> 0.6 (0.2, beyond threshold) but per-fixture deltas swing wildly,
        // so the paired test is not significant at alpha=0.05. The drop cleared the threshold,
        // so it is surfaced as an unverified (possible) regression rather than discarded.
        var flags = _detector.Detect(Scorer,
            [Version(1, 0.8, 0.8, 0.8, 0.8), Version(2, 0.2, 1.0, 0.2, 1.0)]);

        var flag = Assert.Single(flags);
        Assert.Equal(RegressionConfidence.Unverified, flag.Confidence);
        Assert.Equal(4, flag.PairedFixtureCount);
        Assert.NotNull(flag.PValue);
        Assert.True(flag.PValue >= 0.05, $"expected not significant, p={flag.PValue}");
    }

    [Fact]
    public void Improvement_is_never_flagged()
    {
        var flags = _detector.Detect(Scorer,
            [Version(1, 0.5, 0.5, 0.5), Version(2, 0.9, 0.9, 0.9)]);

        Assert.Empty(flags);
    }

    [Fact]
    public void Single_fixture_catastrophic_drop_is_flagged_as_unverified()
    {
        // The dogfood case: the only labeled fixture flips 1.0 -> 0.0. The drop is total and clears
        // the threshold, but n=1 means significance can't be established — so it is surfaced as an
        // unverified (possible) regression instead of being silently hidden.
        var flags = _detector.Detect(Scorer,
            [Version(1, 1.0), Version(2, 0.0)]);

        var flag = Assert.Single(flags);
        Assert.Equal(RegressionConfidence.Unverified, flag.Confidence);
        Assert.Equal(1, flag.PairedFixtureCount);
        Assert.Null(flag.PValue); // n < 2 → significance can't be computed
        Assert.Equal(-1.0, flag.Delta, 3);
    }

    [Fact]
    public void Threshold_is_configurable()
    {
        var versions = new[] { Version(1, 0.90, 0.90, 0.90), Version(2, 0.86, 0.86, 0.86) }; // 0.04 drop

        Assert.Empty(_detector.Detect(Scorer, versions, threshold: 0.05)); // above default threshold
        Assert.Single(_detector.Detect(Scorer, versions, threshold: 0.02)); // now beyond threshold
    }

    [Fact]
    public void Each_consecutive_regression_across_three_versions_is_flagged()
    {
        var flags = _detector.Detect(Scorer,
        [
            Version(1, 0.9, 0.9, 0.9),
            Version(2, 0.7, 0.7, 0.7),
            Version(3, 0.5, 0.5, 0.5),
        ]);

        Assert.Equal(2, flags.Count);
        Assert.Contains(flags, f => f is { FromVersionNumber: 1, ToVersionNumber: 2 });
        Assert.Contains(flags, f => f is { FromVersionNumber: 2, ToVersionNumber: 3 });
    }

    [Fact]
    public void Versions_with_no_overlapping_fixtures_are_skipped()
    {
        var v1 = new VersionScoreSet(Guid.NewGuid(), 1, "v1", Guid.NewGuid(),
            new Dictionary<Guid, double> { [FixtureId(0)] = 0.9 });
        var v2 = new VersionScoreSet(Guid.NewGuid(), 2, "v2", Guid.NewGuid(),
            new Dictionary<Guid, double> { [FixtureId(99)] = 0.1 }); // different fixture

        Assert.Empty(_detector.Detect(Scorer, [v1, v2]));
    }

    // ---- Handler over the repositories (matched by real dataset fixture ids) ----

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
    public async Task Handler_flags_a_significant_regression_between_versions()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1));
        var datasetId = Guid.NewGuid();
        var f = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var scorer = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When,
            [(f[0], 0.9), (f[1], 0.9), (f[2], 0.9), (f[3], 0.9)]));
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddDays(1),
            [(f[0], 0.7), (f[1], 0.7), (f[2], 0.7), (f[3], 0.7)]));

        var handler = new RegressionAnalyticsHandler(runs, promptRepo, new RegressionDetector());
        var flags = await handler.HandleAsync(prompt.Id, datasetId);

        var flag = Assert.Single(flags!);
        Assert.Equal(1, flag.FromVersionNumber);
        Assert.Equal(2, flag.ToVersionNumber);
        Assert.Equal(ScorerKind.LlmJudge, flag.Scorer.Kind);
        Assert.Equal(4, flag.PairedFixtureCount);
        Assert.Equal(RegressionConfidence.Confirmed, flag.Confidence);
    }

    [Fact]
    public async Task Handler_surfaces_a_single_fixture_drop_as_unverified()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1));
        var datasetId = Guid.NewGuid();
        var fixture = Guid.NewGuid();
        var scorer = ScorerDescriptor.LlmJudge("good?", "claude-opus-4-8");

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        // The single labeled fixture flips 1.0 -> 0.0 (the dogfood catastrophe).
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(fixture, 1.0)]));
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddDays(1), [(fixture, 0.0)]));

        var handler = new RegressionAnalyticsHandler(runs, promptRepo, new RegressionDetector());
        var flags = await handler.HandleAsync(prompt.Id, datasetId);

        var flag = Assert.Single(flags!);
        Assert.Equal(RegressionConfidence.Unverified, flag.Confidence);
        Assert.Equal(1, flag.PairedFixtureCount);
        Assert.Null(flag.PValue);
    }

    [Fact]
    public async Task Handler_uses_the_latest_run_of_each_version()
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Summarizer");
        var v1 = prompt.AddVersion("v1", "claude-opus-4-8", When);
        var v2 = prompt.AddVersion("v2", "claude-opus-4-8", When.AddDays(1));
        var datasetId = Guid.NewGuid();
        var f = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var scorer = ScorerDescriptor.Deterministic(ScorerKind.FuzzyMatch);

        var promptRepo = new InMemoryPromptRepo();
        await promptRepo.AddAsync(prompt);
        var runs = new InMemoryEvalRunRepo();
        await runs.AddAsync(RunFor(prompt.Id, v1.Id, datasetId, scorer, When, [(f[0], 0.9), (f[1], 0.9), (f[2], 0.9)]));
        // A stale v2 run shows a big drop; a newer v2 run recovers → no regression should be reported.
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddHours(1), [(f[0], 0.2), (f[1], 0.2), (f[2], 0.2)]));
        await runs.AddAsync(RunFor(prompt.Id, v2.Id, datasetId, scorer, When.AddDays(1), [(f[0], 0.9), (f[1], 0.9), (f[2], 0.9)]));

        var handler = new RegressionAnalyticsHandler(runs, promptRepo, new RegressionDetector());
        var flags = await handler.HandleAsync(prompt.Id, datasetId);

        Assert.Empty(flags!);
    }

    [Fact]
    public async Task Handler_returns_null_for_unknown_prompt()
    {
        var handler = new RegressionAnalyticsHandler(new InMemoryEvalRunRepo(), new InMemoryPromptRepo(), new RegressionDetector());
        Assert.Null(await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
