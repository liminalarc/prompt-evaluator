using Application.Analytics;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class BackportArtifactHandlerTests
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

    // A prompt with a shipped Current v1 and a higher-scoring v2 (the backport target), one regex
    // scorer over one dataset. Returns the assembled repos + the handler + the two versions.
    private static (BackportArtifactHandler Handler, Prompt Prompt, PromptVersion Current, PromptVersion Target)
        BuildEligible(string currentContent, string targetContent, double currentScore, double targetScore)
    {
        var prompt = Prompt.Create(Guid.NewGuid(), "Summarizer");
        var v1 = prompt.AddVersion(currentContent, "claude-sonnet-5", When);
        var v2 = prompt.AddVersion(targetContent, "claude-opus-4-8", When.AddDays(1));
        prompt.SetCurrentVersion(v1.Id, "abc1234", When);

        var promptRepo = new InMemoryPromptRepo(); promptRepo.AddAsync(prompt).Wait();
        var datasetRepo = new InMemoryDatasetRepo();
        var ds = Dataset.Create(prompt.Id, "Golden set"); datasetRepo.AddAsync(ds).Wait();
        var runs = new InMemoryEvalRunRepo();
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^out");
        var f = Guid.NewGuid();
        runs.AddAsync(RunWith(prompt.Id, v1.Id, ds.Id, regex, When, (f, currentScore, true))).Wait();
        runs.AddAsync(RunWith(prompt.Id, v2.Id, ds.Id, regex, When.AddDays(1), (f, targetScore, true))).Wait();

        var scorerConfigs = new InMemoryScorerConfigRepo();
        var status = new VersionStatusHandler(promptRepo, runs, datasetRepo, scorerConfigs, new RegressionDetector());
        var comparison = new ComparisonAnalyticsHandler(runs, promptRepo, datasetRepo);
        var handler = new BackportArtifactHandler(promptRepo, datasetRepo, status, comparison);
        return (handler, prompt, v1, v2);
    }

    [Fact]
    public async Task Unknown_prompt_returns_null()
    {
        var (handler, _, _, _) = BuildEligible("a", "b", 0.5, 0.9);
        Assert.Null(await handler.HandleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task With_no_backport_target_returns_null()
    {
        // Target scores no higher than Current → nothing is eligible → no artifact.
        var (handler, prompt, _, _) = BuildEligible("a", "b", 0.9, 0.5);
        Assert.Null(await handler.HandleAsync(prompt.Id));
    }

    [Fact]
    public async Task Carries_the_target_content_and_metadata()
    {
        var (handler, prompt, current, target) =
            BuildEligible("Summarize: {input}", "Summarize concisely: {input}", 0.5, 0.9);

        var artifact = await handler.HandleAsync(prompt.Id);

        Assert.NotNull(artifact);
        Assert.Equal("Summarizer", artifact!.PromptName);
        Assert.Equal(current.VersionNumber, artifact.CurrentVersionNumber);
        Assert.Equal("abc1234", artifact.CurrentVersionSha);
        Assert.Equal(target.VersionNumber, artifact.TargetVersionNumber);
        Assert.Equal("claude-opus-4-8", artifact.TargetModel);
        Assert.Equal("Summarize concisely: {input}", artifact.Content); // exact-prompt payload
    }

    [Fact]
    public async Task Diff_is_computed_against_current_content()
    {
        var (handler, prompt, _, _) =
            BuildEligible("Summarize: {input}", "Summarize concisely: {input}", 0.5, 0.9);

        var artifact = await handler.HandleAsync(prompt.Id);

        Assert.Contains(artifact!.Diff, l => l is { Kind: DiffLineKind.Removed, Text: "Summarize: {input}" });
        Assert.Contains(artifact.Diff, l => l is { Kind: DiffLineKind.Added, Text: "Summarize concisely: {input}" });
    }

    [Fact]
    public async Task Score_delta_summarizes_target_vs_current_per_scorer()
    {
        var (handler, prompt, _, _) = BuildEligible("a", "b", 0.5, 0.9);

        var artifact = await handler.HandleAsync(prompt.Id);

        var delta = Assert.Single(artifact!.ScoreDeltas);
        Assert.Equal("Golden set", delta.DatasetName);
        Assert.Equal(0.5, delta.CurrentMean, precision: 6);
        Assert.Equal(0.9, delta.TargetMean, precision: 6);
        Assert.Equal(0.4, delta.Delta, precision: 6);
    }

    [Fact]
    public async Task Markdown_carries_the_headline_content_diff_and_deltas()
    {
        var (handler, prompt, _, _) =
            BuildEligible("Summarize: {input}", "Summarize concisely: {input}", 0.5, 0.9);

        var artifact = await handler.HandleAsync(prompt.Id);
        var md = artifact!.Markdown;

        Assert.Contains("Summarizer", md);
        Assert.Contains("v1", md);
        Assert.Contains("v2", md);
        Assert.Contains("claude-opus-4-8", md); // target model
        Assert.Contains("Summarize concisely: {input}", md); // full new content
        Assert.Contains("Golden set", md); // score-delta row
        Assert.Contains("Mark backported", md); // apply checklist
        Assert.EndsWith(".md", artifact.FileName);
    }
}
