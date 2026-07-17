using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application;
using Application.Ports;

namespace Infrastructure.Http;

/// <summary>
/// HTTP adapter for <see cref="IEvaluationRunner"/>. Talks to the Python eval-runner's
/// <c>/echo</c> endpoint. The DTOs here mirror the eval-runner's Pydantic contract.
/// </summary>
public sealed class EvalRunnerClient(HttpClient http) : IEvaluationRunner
{
    public async Task<string> EchoAsync(string prompt, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/echo", new EchoRequest(prompt), ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EchoResponse>(ct);
        return body?.Output
            ?? throw new InvalidOperationException("eval-runner returned an empty /echo body.");
    }

    public async Task<ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
    {
        var body = await http.GetFromJsonAsync<VersionDto>("/version", ct);
        return body is null ? null : new ServiceVersion(body.Service, body.Version, body.Commit);
    }

    public async Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
    {
        // Unreachable eval-runner -> null (availability unknown, so the catalog hides nothing).
        try
        {
            var body = await http.GetFromJsonAsync<ProvidersDto>("/providers", ct);
            return body?.Providers;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
        IReadOnlyList<SeedExampleData> seeds,
        GenerationGuidanceData guidance,
        int count,
        CancellationToken ct = default)
    {
        var request = new GenerateRequest(
            seeds.Select(s => new SeedDto(s.Input, s.UpstreamContext, s.ExpectedOutput)).ToList(),
            new GuidanceDto(guidance.CoverageGoals, guidance.EdgeCases, guidance.Constraints),
            count);

        var response = await http.PostAsJsonAsync("/generate-fixtures", request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GenerateResponse>(ct)
            ?? throw new InvalidOperationException("eval-runner returned an empty /generate-fixtures body.");

        return body.Fixtures
            .Select(f => new GeneratedFixtureData(f.Input, f.UpstreamContext, f.ExpectedOutput, f.SeedIndex))
            .ToList();
    }

    public async Task<PromptExecution> ExecutePromptAsync(
        string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
    {
        var request = new ExecuteRequest(promptContent, targetModel, input, upstreamContext);
        var response = await http.PostAsJsonAsync("/execute-prompt", request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExecuteResponse>(ct)
            ?? throw new InvalidOperationException("eval-runner returned an empty /execute-prompt body.");

        return new PromptExecution(body.Output, body.LatencyMs, body.InputTokens, body.OutputTokens, body.CostUsd);
    }

    public async Task<JudgeVerdict> JudgeAsync(
        string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
    {
        var request = new JudgeRequest(rubric, input, output, expected, judgeModel);
        var response = await http.PostAsJsonAsync("/judge", request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JudgeResponse>(ct)
            ?? throw new InvalidOperationException("eval-runner returned an empty /judge body.");

        return new JudgeVerdict(body.Score, body.Passed, body.Rationale);
    }

    // Serialized with System.Text.Json web defaults (camelCase) -> {"prompt":...} / {"output":...}.
    private sealed record EchoRequest(string Prompt);

    private sealed record EchoResponse(string Output);

    private sealed record VersionDto(string Service, string Version, string Commit);

    private sealed record ProvidersDto(string[] Providers);

    // The generation contract with eval-runner is snake_case (Pydantic-native); these DTOs
    // pin the wire names explicitly since the web-default camelCase would not match.
    private sealed record GenerateRequest(
        [property: JsonPropertyName("seed_examples")] IReadOnlyList<SeedDto> SeedExamples,
        [property: JsonPropertyName("guidance")] GuidanceDto Guidance,
        [property: JsonPropertyName("count")] int Count);

    private sealed record SeedDto(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("upstream_context")] string? UpstreamContext,
        [property: JsonPropertyName("expected_output")] string? ExpectedOutput);

    private sealed record GuidanceDto(
        [property: JsonPropertyName("coverage_goals")] string? CoverageGoals,
        [property: JsonPropertyName("edge_cases")] string? EdgeCases,
        [property: JsonPropertyName("constraints")] string? Constraints);

    private sealed record GenerateResponse(
        [property: JsonPropertyName("fixtures")] IReadOnlyList<GeneratedDto> Fixtures);

    private sealed record GeneratedDto(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("upstream_context")] string? UpstreamContext,
        [property: JsonPropertyName("expected_output")] string? ExpectedOutput,
        [property: JsonPropertyName("seed_index")] int? SeedIndex);

    private sealed record ExecuteRequest(
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("upstream_context")] string? UpstreamContext);

    private sealed record ExecuteResponse(
        [property: JsonPropertyName("output")] string Output,
        [property: JsonPropertyName("latency_ms")] int LatencyMs,
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens,
        [property: JsonPropertyName("cost_usd")] decimal? CostUsd);

    private sealed record JudgeRequest(
        [property: JsonPropertyName("rubric")] string Rubric,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("output")] string Output,
        [property: JsonPropertyName("expected")] string? Expected,
        [property: JsonPropertyName("model")] string Model);

    private sealed record JudgeResponse(
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("passed")] bool Passed,
        [property: JsonPropertyName("rationale")] string Rationale);
}
