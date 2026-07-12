using Domain;

namespace Api.EvalRuns;

public sealed record CreateEvalRunRequest(string Prompt);

public sealed record EvalRunResponse(Guid Id, string Prompt, string Output, DateTimeOffset CreatedAt)
{
    public static EvalRunResponse From(EvalRun run) => new(run.Id, run.Prompt, run.Output, run.CreatedAt);
}
