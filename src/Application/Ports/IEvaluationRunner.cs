namespace Application.Ports;

/// <summary>
/// Port to the Python eval-runner service. In the skeleton it only echoes the prompt;
/// later it grows LLM-judge scoring and synthetic-fixture generation. Python is an
/// execution detail behind this seam — never a domain authority.
/// </summary>
public interface IEvaluationRunner
{
    Task<string> EchoAsync(string prompt, CancellationToken ct = default);

    /// <summary>The eval-runner's self-reported version, or null if unreachable.</summary>
    Task<ServiceVersion?> GetVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Generates <paramref name="count"/> SLM-shaped fixtures, seeded from the captured
    /// <paramref name="seeds"/> so the distribution matches, steered by operator
    /// <paramref name="guidance"/>. Python is an execution detail behind this seam.
    /// </summary>
    Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
        IReadOnlyList<SeedExampleData> seeds,
        GenerationGuidanceData guidance,
        int count,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a prompt version's <paramref name="promptContent"/> against a fixture
    /// <paramref name="input"/> on its <paramref name="targetModel"/>, returning the output plus
    /// the latency/cost of producing it. The subject model is opaque to the domain (1.5 is where
    /// non-Claude execution lands).
    /// </summary>
    Task<PromptExecution> ExecutePromptAsync(
        string promptContent,
        string targetModel,
        string input,
        string? upstreamContext,
        CancellationToken ct = default);

    /// <summary>
    /// Scores an <paramref name="output"/> against a <paramref name="rubric"/> using the given
    /// <paramref name="judgeModel"/>, returning a structured verdict (never parsed from prose).
    /// The judge model is part of the scorer's identity, so it is passed explicitly here.
    /// </summary>
    Task<JudgeVerdict> JudgeAsync(
        string rubric,
        string input,
        string output,
        string? expected,
        string judgeModel,
        CancellationToken ct = default);
}
