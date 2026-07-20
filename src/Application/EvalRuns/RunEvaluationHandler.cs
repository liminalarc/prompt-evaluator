using Application.Ports;
using Application.Scoring;
using Domain;

namespace Application.EvalRuns;

/// <summary>
/// The core use case: run a prompt version over a dataset and score every fixture. For each fixture
/// it executes the prompt on its target model (via the eval-runner), captures the output plus
/// latency/cost, then applies every scorer configured for the dataset. The assembled
/// <see cref="EvalRun"/> is persisted append-only — nothing prior is overwritten.
/// </summary>
public sealed class RunEvaluationHandler(
    IPromptRepository prompts,
    IDatasetRepository datasets,
    IScorerConfigRepository scorerConfigs,
    IEvaluationRunner runner,
    ScorerFactory scorerFactory,
    IEvalRunRepository runs,
    TimeProvider time,
    ICurrentUser currentUser,
    IAiUsageContextAccessor usageContext)
{
    /// <summary>Returns null when the prompt, version, or dataset does not exist (Api → 404).</summary>
    public async Task<EvalRun?> HandleAsync(
        Guid promptId, Guid promptVersionId, Guid datasetId, CancellationToken ct = default)
    {
        var prompt = await prompts.GetByIdAsync(promptId, ct);
        var version = prompt?.Versions.FirstOrDefault(v => v.Id == promptVersionId);
        if (prompt is null || version is null)
            return null;

        var dataset = await datasets.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            return null;

        // A dataset lives with exactly one prompt (1.7) — refuse to run it against another prompt.
        if (dataset.PromptId != promptId)
            return null;

        var configs = await scorerConfigs.ListByDatasetAsync(datasetId, ct);
        var scorers = configs.Select(c => scorerFactory.Create(c.Scorer)).ToList();

        var run = EvalRun.Create(promptId, promptVersionId, datasetId, time.GetUtcNow());

        // Attribute every eval-runner call in this run (execute + judge-via-scorer) to the prompt's
        // org and the calling user. Ambient so the judge call, which runs through IScorer, is covered.
        using var _ = usageContext.Begin(new AiUsageAttribution(prompt.OrganizationId, currentUser.UserId));

        foreach (var fixture in dataset.Fixtures)
        {
            var execution = await runner.ExecutePromptAsync(
                version.Content, version.TargetModel, fixture.Input, fixture.UpstreamContext, ct);

            var fixtureRun = run.RecordFixture(
                fixture.Id, execution.Output, execution.LatencyMs,
                execution.InputTokens, execution.OutputTokens, execution.CostUsd);

            var context = new ScoringContext(
                fixture.Input, fixture.ExpectedOutput, execution.Output, execution.LatencyMs, execution.CostUsd);

            foreach (var scorer in scorers)
            {
                var outcome = await scorer.ScoreAsync(context, ct);
                fixtureRun.AddScore(scorer.Descriptor, outcome.Value, outcome.Passed, outcome.Detail);
            }
        }

        await runs.AddAsync(run, ct);
        return run;
    }
}
