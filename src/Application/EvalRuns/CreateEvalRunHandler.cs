using Application.Ports;
using Domain;

namespace Application.EvalRuns;

/// <summary>
/// Use case for the walking-skeleton round trip: send a prompt to the eval-runner, wrap the
/// echoed output in an <see cref="EvalRun"/>, and persist it.
/// </summary>
public sealed class CreateEvalRunHandler(
    IEvaluationRunner runner,
    IEvalRunRepository repository,
    TimeProvider timeProvider)
{
    public async Task<EvalRun> HandleAsync(string prompt, CancellationToken ct = default)
    {
        var output = await runner.EchoAsync(prompt, ct);
        var run = EvalRun.Create(prompt, output, timeProvider.GetUtcNow());
        await repository.AddAsync(run, ct);
        return run;
    }
}
