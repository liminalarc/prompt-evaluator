using Application.Datasets;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class DatasetHandlersTests
{
    private sealed class InMemoryDatasetRepo : IDatasetRepository
    {
        public readonly List<Dataset> Saved = [];
        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Dataset dataset, CancellationToken ct = default)
        {
            Saved.Add(dataset);
            return Task.CompletedTask;
        }

        public Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(d => d.Id == id));

        public Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Dataset>>(Saved);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset When = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateDataset_persists_and_returns_the_dataset()
    {
        var repo = new InMemoryDatasetRepo();
        var handler = new CreateDatasetHandler(repo);

        var dataset = await handler.HandleAsync("Summaries", "captured summaries");

        Assert.Equal("Summaries", dataset.Name);
        Assert.Single(repo.Saved);
        Assert.Equal(dataset.Id, repo.Saved[0].Id);
    }

    [Fact]
    public async Task CaptureFixtures_lands_captured_tuples_mapped_and_saved()
    {
        var repo = new InMemoryDatasetRepo();
        var existing = Dataset.Create("Summaries");
        await repo.AddAsync(existing);
        var handler = new CaptureFixturesHandler(repo, new FixtureRedactor(), new FixedTime(When));

        var updated = await handler.HandleAsync(existing.Id, new[]
        {
            new CapturedTuple("summarize this", "raw slm output", "the summary"),
            new CapturedTuple("classify this", null, null),
        });

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Fixtures.Count);
        Assert.All(updated.Fixtures, f => Assert.Equal(FixtureOrigin.Captured, f.Origin));
        var first = updated.Fixtures[0];
        Assert.Equal("summarize this", first.Input);
        Assert.Equal("raw slm output", first.UpstreamContext);
        Assert.Equal("the summary", first.ExpectedOutput);
        Assert.Null(first.SeedFixtureId);
        Assert.Equal(When, first.CreatedAt);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task CaptureFixtures_redacts_pii_before_persisting()
    {
        var repo = new InMemoryDatasetRepo();
        var existing = Dataset.Create("Summaries");
        await repo.AddAsync(existing);
        var handler = new CaptureFixturesHandler(repo, new FixtureRedactor(), new FixedTime(When));

        var updated = await handler.HandleAsync(existing.Id, new[]
        {
            new CapturedTuple(
                "Contact alice@example.com about the order",
                "call +1 415 555 0132 for details",
                "resolved"),
        });

        var fixture = Assert.Single(updated!.Fixtures);
        Assert.DoesNotContain("alice@example.com", fixture.Input);
        Assert.Contains("[REDACTED-EMAIL]", fixture.Input);
        Assert.DoesNotContain("555 0132", fixture.UpstreamContext!);
        Assert.Contains("[REDACTED-PHONE]", fixture.UpstreamContext!);
    }

    [Fact]
    public async Task CaptureFixtures_returns_null_when_the_dataset_does_not_exist()
    {
        var repo = new InMemoryDatasetRepo();
        var handler = new CaptureFixturesHandler(repo, new FixtureRedactor(), new FixedTime(When));

        var result = await handler.HandleAsync(
            Guid.NewGuid(), new[] { new CapturedTuple("x", null, null) });

        Assert.Null(result);
        Assert.Equal(0, repo.SaveChangesCalls);
    }

    [Fact]
    public void FixtureRedactor_masks_email_and_phone_and_passes_null_through()
    {
        var redactor = new FixtureRedactor();

        Assert.Equal("[REDACTED-EMAIL] wrote in", redactor.Redact("bob@work.co wrote in"));
        Assert.Contains("[REDACTED-PHONE]", redactor.Redact("ring 212-555-0147 now"));
        Assert.Null(redactor.Redact(null));
    }

    private sealed class RecordingRunner(IReadOnlyList<GeneratedFixtureData> result) : IEvaluationRunner
    {
        public IReadOnlyList<SeedExampleData>? Seeds { get; private set; }
        public GenerationGuidanceData? Guidance { get; private set; }
        public int Count { get; private set; }

        public Task<string> EchoAsync(string prompt, CancellationToken ct = default) => Task.FromResult(prompt);
        public Task<Application.ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => Task.FromResult<Application.ServiceVersion?>(null);

        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
        {
            Seeds = seeds;
            Guidance = guidance;
            Count = count;
            return Task.FromResult(result);
        }

        public Task<PromptExecution> ExecutePromptAsync(string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<JudgeVerdict> JudgeAsync(string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static readonly GenerationGuidanceData Guidance =
        new("cover long inputs", "empty/adversarial", "under 200 tokens");

    [Fact]
    public async Task GenerateSynthetic_seeds_from_captured_links_the_seed_and_saves()
    {
        var repo = new InMemoryDatasetRepo();
        var dataset = Dataset.Create("Summaries");
        dataset.AddCapturedFixture("captured 0", When);
        var seed1 = dataset.AddCapturedFixture("captured 1", When);
        await repo.AddAsync(dataset);

        var runner = new RecordingRunner(new[]
        {
            new GeneratedFixtureData("generated variant", "slm-shaped", null, SeedIndex: 1),
        });
        var handler = new GenerateSyntheticFixturesHandler(repo, runner, new FixedTime(When));

        var updated = await handler.HandleAsync(dataset.Id, Guidance, count: 3);

        Assert.NotNull(updated);
        // Both captured fixtures were passed as seeds; operator guidance + count forwarded.
        Assert.Equal(2, runner.Seeds!.Count);
        Assert.Equal(Guidance, runner.Guidance);
        Assert.Equal(3, runner.Count);

        var synthetic = Assert.Single(updated!.Fixtures, f => f.Origin == FixtureOrigin.Synthetic);
        Assert.Equal("generated variant", synthetic.Input);
        Assert.Equal("slm-shaped", synthetic.UpstreamContext);
        Assert.Equal(seed1.Id, synthetic.SeedFixtureId);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task GenerateSynthetic_falls_back_to_first_seed_when_index_is_null_or_out_of_range()
    {
        var repo = new InMemoryDatasetRepo();
        var dataset = Dataset.Create("Summaries");
        var seed0 = dataset.AddCapturedFixture("captured 0", When);
        await repo.AddAsync(dataset);

        var runner = new RecordingRunner(new[]
        {
            new GeneratedFixtureData("no attribution", null, null, SeedIndex: null),
            new GeneratedFixtureData("bad attribution", null, null, SeedIndex: 99),
        });
        var handler = new GenerateSyntheticFixturesHandler(repo, runner, new FixedTime(When));

        var updated = await handler.HandleAsync(dataset.Id, Guidance, count: 2);

        var synthetics = updated!.Fixtures.Where(f => f.Origin == FixtureOrigin.Synthetic).ToList();
        Assert.Equal(2, synthetics.Count);
        Assert.All(synthetics, s => Assert.Equal(seed0.Id, s.SeedFixtureId));
    }

    [Fact]
    public async Task GenerateSynthetic_skips_blank_input_variants_and_persists_the_rest()
    {
        var repo = new InMemoryDatasetRepo();
        var dataset = Dataset.Create("Summaries");
        dataset.AddCapturedFixture("captured 0", When);
        await repo.AddAsync(dataset);

        var runner = new RecordingRunner(new[]
        {
            new GeneratedFixtureData("valid variant", null, null, 0),
            new GeneratedFixtureData("   ", null, null, 0),  // invalid: blank input
        });
        var handler = new GenerateSyntheticFixturesHandler(repo, runner, new FixedTime(When));

        var updated = await handler.HandleAsync(dataset.Id, Guidance, count: 2);

        var synthetic = Assert.Single(updated!.Fixtures, f => f.Origin == FixtureOrigin.Synthetic);
        Assert.Equal("valid variant", synthetic.Input);
    }

    [Fact]
    public async Task GenerateSynthetic_throws_when_no_captured_fixtures_to_seed_from()
    {
        var repo = new InMemoryDatasetRepo();
        var dataset = Dataset.Create("Empty");
        await repo.AddAsync(dataset);
        var runner = new RecordingRunner([]);
        var handler = new GenerateSyntheticFixturesHandler(repo, runner, new FixedTime(When));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(dataset.Id, Guidance, count: 1));
    }

    [Fact]
    public async Task GenerateSynthetic_returns_null_when_the_dataset_does_not_exist()
    {
        var repo = new InMemoryDatasetRepo();
        var runner = new RecordingRunner([]);
        var handler = new GenerateSyntheticFixturesHandler(repo, runner, new FixedTime(When));

        var result = await handler.HandleAsync(Guid.NewGuid(), Guidance, count: 1);

        Assert.Null(result);
    }
}
