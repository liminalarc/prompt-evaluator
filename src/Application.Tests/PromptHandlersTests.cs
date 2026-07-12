using Application.Prompts;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class PromptHandlersTests
{
    private sealed class InMemoryPromptRepo : IPromptRepository
    {
        public readonly List<Prompt> Saved = [];
        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Prompt prompt, CancellationToken ct = default)
        {
            Saved.Add(prompt);
            return Task.CompletedTask;
        }

        public Task<Prompt?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(p => p.Id == id));

        public Task<IReadOnlyList<Prompt>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Prompt>>(Saved);

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
    public async Task CreatePrompt_persists_and_returns_the_prompt()
    {
        var repo = new InMemoryPromptRepo();
        var handler = new CreatePromptHandler(repo);

        var prompt = await handler.HandleAsync("Summarizer", "Summarizes captured output");

        Assert.Equal("Summarizer", prompt.Name);
        Assert.Equal("Summarizes captured output", prompt.Description);
        Assert.Single(repo.Saved);
        Assert.Equal(prompt.Id, repo.Saved[0].Id);
    }

    [Fact]
    public async Task AddPromptVersion_appends_with_the_clock_time_and_saves()
    {
        var repo = new InMemoryPromptRepo();
        var existing = Prompt.Create("Summarizer");
        await repo.AddAsync(existing);
        var handler = new AddPromptVersionHandler(repo, new FixedTime(When));

        var updated = await handler.HandleAsync(
            existing.Id, "Summarize: {input}", "claude-sonnet-5", label: "baseline", sourceApp: "Stormboard");

        Assert.NotNull(updated);
        var version = Assert.Single(updated!.Versions);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("Summarize: {input}", version.Content);
        Assert.Equal("claude-sonnet-5", version.TargetModel);
        Assert.Equal("baseline", version.Label);
        Assert.Equal("Stormboard", version.SourceApp);
        Assert.Equal(When, version.CreatedAt);
        Assert.Equal(1, repo.SaveChangesCalls);
    }

    [Fact]
    public async Task AddPromptVersion_returns_null_when_the_prompt_does_not_exist()
    {
        var repo = new InMemoryPromptRepo();
        var handler = new AddPromptVersionHandler(repo, new FixedTime(When));

        var result = await handler.HandleAsync(
            Guid.NewGuid(), "Summarize: {input}", "claude-sonnet-5", label: null, sourceApp: null);

        Assert.Null(result);
        Assert.Equal(0, repo.SaveChangesCalls);
    }
}
