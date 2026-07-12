namespace Domain;

/// <summary>
/// The kinds of scorer the harness can apply to a fixture's output. Deterministic kinds run
/// in-process; <see cref="LlmJudge"/> is delegated to the eval-runner. Latency and cost are
/// modelled as scorers too, so every per-fixture measurement is a uniform <see cref="Score"/>.
/// </summary>
public enum ScorerKind
{
    Regex,
    JsonSchema,
    ExactMatch,
    FuzzyMatch,
    Latency,
    Cost,
    LlmJudge,
}
