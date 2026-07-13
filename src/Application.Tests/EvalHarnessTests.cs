using Application.Datasets;
using Application.EvalRuns;
using Application.Ports;
using Application.Scoring;
using Domain;

namespace Application.Tests;

public class EvalHarnessTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Scripted eval-runner: execution echoes "OUT:{input}"; the judge encodes the judge model in
    // its verdict so tests can prove the model reached the seam.
    private sealed class ScriptedRunner : IEvaluationRunner
    {
        public List<string> ExecutedInputs { get; } = [];
        public List<string> JudgeModels { get; } = [];

        public Task<string> EchoAsync(string prompt, CancellationToken ct = default) => Task.FromResult(prompt);
        public Task<Application.ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => Task.FromResult<Application.ServiceVersion?>(null);
        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GeneratedFixtureData>>([]);

        public Task<PromptExecution> ExecutePromptAsync(
            string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
        {
            ExecutedInputs.Add(input);
            return Task.FromResult(new PromptExecution($"OUT:{input}", LatencyMs: 100, CostUsd: 0.001m));
        }

        public Task<JudgeVerdict> JudgeAsync(
            string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
        {
            JudgeModels.Add(judgeModel);
            var score = judgeModel == "claude-opus-4-8" ? 0.9 : 0.5;
            return Task.FromResult(new JudgeVerdict(score, Passed: true, Rationale: $"judged by {judgeModel}"));
        }
    }

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

    private sealed class InMemoryDatasetRepo : IDatasetRepository
    {
        private readonly List<Dataset> _saved = [];
        public Task AddAsync(Dataset dataset, CancellationToken ct = default) { _saved.Add(dataset); return Task.CompletedTask; }
        public Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_saved.SingleOrDefault(d => d.Id == id));
        public Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Dataset>>(_saved);
        public Task<IReadOnlyList<Dataset>> ListByPromptAsync(Guid promptId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Dataset>>(_saved.Where(d => d.PromptId == promptId).ToList());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryScorerConfigRepo : IScorerConfigRepository
    {
        public readonly List<ScorerConfig> Saved = [];
        public Task AddAsync(ScorerConfig config, CancellationToken ct = default) { Saved.Add(config); return Task.CompletedTask; }
        public Task<IReadOnlyList<ScorerConfig>> ListByDatasetAsync(Guid datasetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ScorerConfig>>(Saved.Where(c => c.DatasetId == datasetId).ToList());
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

    private sealed record Harness(
        RunEvaluationHandler Handler,
        ScriptedRunner Runner,
        InMemoryEvalRunRepo Runs,
        InMemoryScorerConfigRepo ScorerConfigs,
        Prompt Prompt,
        PromptVersion Version,
        Dataset Dataset);

    private static async Task<Harness> BuildAsync(params ScorerDescriptor[] scorers)
    {
        var promptRepo = new InMemoryPromptRepo();
        var datasetRepo = new InMemoryDatasetRepo();
        var scorerRepo = new InMemoryScorerConfigRepo();
        var runRepo = new InMemoryEvalRunRepo();
        var runner = new ScriptedRunner();

        var prompt = Prompt.Create("Summarizer");
        var version = prompt.AddVersion("You summarize text.", "claude-opus-4-8", When);
        await promptRepo.AddAsync(prompt);

        var dataset = Dataset.Create(prompt.Id, "Summaries");
        dataset.AddCapturedFixture("hello world", When, upstreamContext: "raw slm output");
        dataset.AddCapturedFixture("foo bar", When);
        await datasetRepo.AddAsync(dataset);

        foreach (var scorer in scorers)
            await scorerRepo.AddAsync(ScorerConfig.Create(dataset.Id, scorer, When));

        var handler = new RunEvaluationHandler(
            promptRepo, datasetRepo, scorerRepo, runner, new ScorerFactory(runner), runRepo, new FixedTime(When));

        return new Harness(handler, runner, runRepo, scorerRepo, prompt, version, dataset);
    }

    [Fact]
    public async Task Run_executes_every_fixture_and_persists_a_score_per_fixture_per_scorer()
    {
        var regex = ScorerDescriptor.Deterministic(ScorerKind.Regex, "^OUT:");
        var judge = ScorerDescriptor.LlmJudge("Is it a good summary?", "claude-opus-4-8");
        var h = await BuildAsync(regex, judge);

        var run = await h.Handler.HandleAsync(h.Prompt.Id, h.Version.Id, h.Dataset.Id);

        Assert.NotNull(run);
        Assert.Equal(h.Prompt.Id, run!.PromptId);
        Assert.Equal(h.Version.Id, run.PromptVersionId);
        Assert.Equal(2, run.Results.Count); // one FixtureRun per fixture

        foreach (var fixtureRun in run.Results)
        {
            Assert.StartsWith("OUT:", fixtureRun.ModelOutput);
            Assert.Equal(100, fixtureRun.LatencyMs);
            Assert.Equal(0.001m, fixtureRun.CostUsd);
            Assert.Equal(2, fixtureRun.Scores.Count); // deterministic + judge compose (AC #4)
            Assert.All(fixtureRun.Scores.Where(s => s.Scorer.Kind == ScorerKind.Regex), s => Assert.Equal(1.0, s.Value));
            var judged = Assert.Single(fixtureRun.Scores, s => s.Scorer.Kind == ScorerKind.LlmJudge);
            Assert.Equal(0.9, judged.Value);
            Assert.Contains("claude-opus-4-8", judged.Detail);
        }

        var persisted = Assert.Single(h.Runs.Saved);
        Assert.Equal(run.Id, persisted.Id);
    }

    [Fact]
    public async Task Reruns_are_append_only_and_never_overwrite_a_prior_run()
    {
        var h = await BuildAsync(ScorerDescriptor.Deterministic(ScorerKind.Regex, "^OUT:"));

        var first = await h.Handler.HandleAsync(h.Prompt.Id, h.Version.Id, h.Dataset.Id);
        var second = await h.Handler.HandleAsync(h.Prompt.Id, h.Version.Id, h.Dataset.Id);

        Assert.NotEqual(first!.Id, second!.Id);
        Assert.Equal(2, h.Runs.Saved.Count);
    }

    [Fact]
    public async Task Different_judge_models_record_as_different_scorer_series()
    {
        // AC #7: judge model is part of scorer identity → distinct series in the same run.
        var opus = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");
        var haiku = ScorerDescriptor.LlmJudge("rubric", "claude-haiku-4-5");
        var h = await BuildAsync(opus, haiku);

        var run = await h.Handler.HandleAsync(h.Prompt.Id, h.Version.Id, h.Dataset.Id);

        var fixtureRun = run!.Results[0];
        var judgeScores = fixtureRun.Scores.Where(s => s.Scorer.Kind == ScorerKind.LlmJudge).ToList();
        Assert.Equal(2, judgeScores.Count);
        Assert.Equal(2, judgeScores.Select(s => s.Scorer.Identity).Distinct().Count());
        Assert.Contains(judgeScores, s => s.Scorer.JudgeModel == "claude-opus-4-8" && s.Value == 0.9);
        Assert.Contains(judgeScores, s => s.Scorer.JudgeModel == "claude-haiku-4-5" && s.Value == 0.5);
    }

    [Fact]
    public async Task Run_returns_null_when_the_prompt_version_or_dataset_is_missing()
    {
        var h = await BuildAsync(ScorerDescriptor.Deterministic(ScorerKind.Regex, "^OUT:"));

        Assert.Null(await h.Handler.HandleAsync(Guid.NewGuid(), h.Version.Id, h.Dataset.Id));
        Assert.Null(await h.Handler.HandleAsync(h.Prompt.Id, Guid.NewGuid(), h.Dataset.Id));
        Assert.Null(await h.Handler.HandleAsync(h.Prompt.Id, h.Version.Id, Guid.NewGuid()));
        Assert.Empty(h.Runs.Saved);
    }

    // ---- LlmJudgeScorer directly (AC #3, Application side) ----

    [Fact]
    public async Task LlmJudgeScorer_delegates_to_the_runner_and_maps_the_verdict()
    {
        var runner = new ScriptedRunner();
        var descriptor = ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8");
        var scorer = new ScorerFactory(runner).Create(descriptor);

        var outcome = await scorer.ScoreAsync(
            new ScoringContext("in", "expected", "model output", 100, 0.001m));

        Assert.Equal(0.9, outcome.Value);
        Assert.True(outcome.Passed);
        Assert.Equal("judged by claude-opus-4-8", outcome.Detail);
        Assert.Contains("claude-opus-4-8", runner.JudgeModels);
    }

    [Fact]
    public async Task Run_rejects_a_dataset_owned_by_a_different_prompt()
    {
        // Datasets live with exactly one prompt (1.7): running prompt A against prompt B's dataset
        // must be refused (Api → 404), never silently scored against the wrong owner.
        var promptRepo = new InMemoryPromptRepo();
        var datasetRepo = new InMemoryDatasetRepo();
        var runRepo = new InMemoryEvalRunRepo();
        var runner = new ScriptedRunner();

        var prompt = Prompt.Create("Summarizer");
        var version = prompt.AddVersion("You summarize text.", "claude-opus-4-8", When);
        await promptRepo.AddAsync(prompt);

        var otherPromptsDataset = Dataset.Create(Guid.NewGuid(), "Someone else's data");
        otherPromptsDataset.AddCapturedFixture("hello", When);
        await datasetRepo.AddAsync(otherPromptsDataset);

        var handler = new RunEvaluationHandler(
            promptRepo, datasetRepo, new InMemoryScorerConfigRepo(), runner,
            new ScorerFactory(runner), runRepo, new FixedTime(When));

        var run = await handler.HandleAsync(prompt.Id, version.Id, otherPromptsDataset.Id);

        Assert.Null(run);
        Assert.Empty(runRepo.Saved);
        Assert.Empty(runner.ExecutedInputs);
    }

    // ---- ConfigureDatasetScorersHandler ----

    [Fact]
    public async Task ConfigureDatasetScorers_persists_and_lists_scorers_for_the_dataset()
    {
        var datasetRepo = new InMemoryDatasetRepo();
        var scorerRepo = new InMemoryScorerConfigRepo();
        var dataset = Dataset.Create(Guid.NewGuid(), "DS");
        await datasetRepo.AddAsync(dataset);
        var handler = new ConfigureDatasetScorersHandler(datasetRepo, scorerRepo, new FixedTime(When));

        var added = await handler.HandleAsync(dataset.Id, ScorerDescriptor.LlmJudge("rubric", "claude-opus-4-8"));

        Assert.NotNull(added);
        Assert.Equal(dataset.Id, added!.DatasetId);
        var listed = Assert.Single(await handler.ListAsync(dataset.Id));
        Assert.Equal(ScorerKind.LlmJudge, listed.Scorer.Kind);
    }

    [Fact]
    public async Task ConfigureDatasetScorers_returns_null_when_the_dataset_does_not_exist()
    {
        var scorerRepo = new InMemoryScorerConfigRepo();
        var handler = new ConfigureDatasetScorersHandler(new InMemoryDatasetRepo(), scorerRepo, new FixedTime(When));

        var result = await handler.HandleAsync(Guid.NewGuid(), ScorerDescriptor.Deterministic(ScorerKind.ExactMatch));

        Assert.Null(result);
        Assert.Empty(scorerRepo.Saved);
    }
}
