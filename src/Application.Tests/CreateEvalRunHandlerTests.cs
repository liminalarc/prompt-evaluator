using Application.EvalRuns;
using Application.Ports;
using Domain;

namespace Application.Tests;

public class CreateEvalRunHandlerTests
{
    private sealed class EchoRunner : IEvaluationRunner
    {
        public Task<string> EchoAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(prompt);
    }

    private sealed class InMemoryRepo : IEvalRunRepository
    {
        public readonly List<EvalRun> Saved = [];
        public Task AddAsync(EvalRun run, CancellationToken ct = default)
        {
            Saved.Add(run);
            return Task.CompletedTask;
        }

        public Task<EvalRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Saved.SingleOrDefault(r => r.Id == id));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Handle_echoes_prompt_and_persists_the_run()
    {
        var when = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var repo = new InMemoryRepo();
        var handler = new CreateEvalRunHandler(new EchoRunner(), repo, new FixedTime(when));

        var result = await handler.HandleAsync("round trip", CancellationToken.None);

        Assert.Equal("round trip", result.Prompt);
        Assert.Equal("round trip", result.Output);
        Assert.Equal(when, result.CreatedAt);
        Assert.Single(repo.Saved);
        Assert.Equal(result.Id, repo.Saved[0].Id);
    }
}
