namespace Application.Ports;

/// <summary>The result of running a prompt version against one fixture input on its target model.</summary>
public sealed record PromptExecution(string Output, int LatencyMs, decimal? CostUsd);

/// <summary>A structured LLM-judge verdict for one output — never parsed from prose.</summary>
public sealed record JudgeVerdict(double Score, bool Passed, string Rationale);
