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
}
