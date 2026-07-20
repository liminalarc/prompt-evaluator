using Application.Ports;
using Domain;

namespace Application.Datasets;

/// <summary>
/// Generates synthetic fixtures for a dataset: takes its captured fixtures as seeds, calls the
/// eval-runner (guided by the operator), and persists the results as
/// <see cref="FixtureOrigin.Synthetic"/> fixtures, each linked back to the captured seed it was
/// derived from. Returns null when the dataset does not exist (Api → 404). Throws
/// <see cref="ArgumentException"/> when there are no captured fixtures to seed from (Api → 400).
/// </summary>
public sealed class GenerateSyntheticFixturesHandler(
    IDatasetRepository repository,
    IPromptRepository prompts,
    IEvaluationRunner runner,
    TimeProvider time,
    ICurrentUser currentUser,
    IAiUsageContextAccessor usageContext)
{
    public async Task<Dataset?> HandleAsync(
        Guid datasetId,
        GenerationGuidanceData guidance,
        int count,
        CancellationToken ct = default)
    {
        var dataset = await repository.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            return null;

        var capturedSeeds = dataset.Fixtures.Where(f => f.Origin == FixtureOrigin.Captured).ToList();
        if (capturedSeeds.Count == 0)
            throw new ArgumentException("Dataset has no captured fixtures to seed synthetic generation.");

        var seeds = capturedSeeds
            .Select(f => new SeedExampleData(f.Input, f.UpstreamContext, f.ExpectedOutput))
            .ToList();

        // Attribute the generation call to the owning prompt's org (dataset → prompt) and the caller.
        var prompt = await prompts.GetByIdAsync(dataset.PromptId, ct);
        using var _ = usageContext.Begin(new AiUsageAttribution(prompt?.OrganizationId, currentUser.UserId));

        var generated = await runner.GenerateSyntheticFixturesAsync(seeds, guidance, count, ct);

        var now = time.GetUtcNow();
        foreach (var g in generated)
        {
            // The generator is an LLM, so a result can violate a domain invariant (e.g. an
            // "empty input" edge-case variant). Skip those defensively rather than letting one
            // bad item abort the whole batch — the valid generated fixtures still persist.
            if (string.IsNullOrWhiteSpace(g.Input))
                continue;

            // Resolve the seed link; fall back to the first captured seed if the generator
            // gave no (or an out-of-range) attribution, since a synthetic fixture must name one.
            var seedIndex = g.SeedIndex is int idx && idx >= 0 && idx < capturedSeeds.Count ? idx : 0;
            dataset.AddSyntheticFixture(
                g.Input,
                capturedSeeds[seedIndex].Id,
                now,
                upstreamContext: g.UpstreamContext,
                expectedOutput: g.ExpectedOutput);
        }

        await repository.SaveChangesAsync(ct);
        return dataset;
    }
}
