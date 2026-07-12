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
}
