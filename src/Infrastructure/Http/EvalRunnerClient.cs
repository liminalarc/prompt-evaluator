using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application;
using Application.Ports;
using Domain;

namespace Infrastructure.Http;

/// <summary>
/// HTTP adapter for <see cref="IEvaluationRunner"/>. Talks to the Python eval-runner. The DTOs here
/// mirror the eval-runner's Pydantic contract.
/// <para>
/// This is the single seam every model call funnels through (subject execution, LLM-judge, synthetic
/// generation), so it is where the AI-usage ledger is captured (6.1): each call records an
/// <see cref="AiUsageRecord"/> on success <em>and</em> failure, attributed via the ambient
/// <see cref="IAiUsageContextAccessor"/> a handler set. Recording is best-effort — it never breaks an
/// eval call.
/// </para>
/// </summary>
public sealed class EvalRunnerClient : IEvaluationRunner
{
    private readonly HttpClient _http;
    private readonly IAiUsageRecorder? _recorder;
    private readonly IAiUsageContextAccessor? _usageContext;
    private readonly TimeProvider _time;

    // The recorder/context/time are optional so a bare `new EvalRunnerClient(http)` (used widely in
    // tests that don't care about the ledger) still compiles; DI injects all four in production.
    public EvalRunnerClient(
        HttpClient http,
        IAiUsageRecorder? recorder = null,
        IAiUsageContextAccessor? usageContext = null,
        TimeProvider? time = null)
    {
        _http = http;
        _recorder = recorder;
        _usageContext = usageContext;
        _time = time ?? TimeProvider.System;
    }

    public async Task<string> EchoAsync(string prompt, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/echo", new EchoRequest(prompt), ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EchoResponse>(ct);
        return body?.Output
            ?? throw new InvalidOperationException("eval-runner returned an empty /echo body.");
    }

    public async Task<ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
    {
        var body = await _http.GetFromJsonAsync<VersionDto>("/version", ct);
        return body is null ? null : new ServiceVersion(body.Service, body.Version, body.Commit);
    }

    public async Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
    {
        // Unreachable eval-runner -> null (availability unknown, so the catalog hides nothing).
        try
        {
            var body = await _http.GetFromJsonAsync<ProvidersDto>("/providers", ct);
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

        var start = _time.GetTimestamp();
        var response = await PostRecordingFailureAsync(
            "/generate-fixtures", request, AiUsageFeature.SyntheticGeneration, requestedModel: null, start, ct);
        var body = await response.Content.ReadFromJsonAsync<GenerateResponse>(ct)
            ?? throw new InvalidOperationException("eval-runner returned an empty /generate-fixtures body.");

        await RecordAsync(AiUsageFeature.SyntheticGeneration, requestedModel: null, body.Usage, AiUsageStatus.Success, start, ct);

        return body.Fixtures
            .Select(f => new GeneratedFixtureData(f.Input, f.UpstreamContext, f.ExpectedOutput, f.SeedIndex))
            .ToList();
    }

    public async Task<PromptExecution> ExecutePromptAsync(
        string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
    {
        var request = new ExecuteRequest(promptContent, targetModel, input, upstreamContext);
        var start = _time.GetTimestamp();
        var response = await PostRecordingFailureAsync(
            "/execute-prompt", request, AiUsageFeature.SubjectExecution, targetModel, start, ct);
        var body = await response.Content.ReadFromJsonAsync<ExecuteResponse>(ct)
            ?? throw new InvalidOperationException("eval-runner returned an empty /execute-prompt body.");

        await RecordAsync(AiUsageFeature.SubjectExecution, targetModel, body.Usage, AiUsageStatus.Success, start, ct);

        return new PromptExecution(body.Output, body.LatencyMs, body.InputTokens, body.OutputTokens, body.CostUsd);
    }

    public async Task<JudgeVerdict> JudgeAsync(
        string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
    {
        var request = new JudgeRequest(rubric, input, output, expected, judgeModel);
        var start = _time.GetTimestamp();
        var response = await PostRecordingFailureAsync(
            "/judge", request, AiUsageFeature.LlmJudge, judgeModel, start, ct);
        var body = await response.Content.ReadFromJsonAsync<JudgeResponse>(ct)
            ?? throw new InvalidOperationException("eval-runner returned an empty /judge body.");

        await RecordAsync(AiUsageFeature.LlmJudge, judgeModel, body.Usage, AiUsageStatus.Success, start, ct);

        return new JudgeVerdict(body.Score, body.Passed, body.Rationale);
    }

    /// <summary>
    /// POSTs a model-call request; on a transport failure or non-success status, records a failed
    /// usage row (a failed call still incurs cost / signals waste — 6.1) before surfacing the error.
    /// Returns the successful response for the caller to deserialize.
    /// </summary>
    private async Task<HttpResponseMessage> PostRecordingFailureAsync<TRequest>(
        string path, TRequest request, AiUsageFeature feature, string? requestedModel, long start, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(path, request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordAsync(feature, requestedModel, usage: null, AiUsageStatus.Error, start, ct);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            await RecordAsync(feature, requestedModel, usage: null, AiUsageStatus.Error, start, ct);
            await EnsureRunnerSuccessAsync(response, ct); // always throws for a non-success status
        }

        return response;
    }

    private async Task RecordAsync(
        AiUsageFeature feature, string? requestedModel, UsageDto? usage, AiUsageStatus fallbackStatus, long start, CancellationToken ct)
    {
        if (_recorder is null)
            return;

        try
        {
            var attribution = _usageContext?.Current ?? default;
            var status = usage?.Status is { } s ? ParseStatus(s) : fallbackStatus;
            var model = string.IsNullOrWhiteSpace(usage?.Model) ? (requestedModel ?? "unknown") : usage!.Model!;
            var latencyMs = (int)_time.GetElapsedTime(start).TotalMilliseconds;

            var record = AiUsageRecord.Create(
                model, feature, status,
                attribution.OrganizationId, attribution.UserId,
                usage?.InputTokens ?? 0, usage?.OutputTokens ?? 0,
                usage?.CacheCreationInputTokens ?? 0, usage?.CacheReadInputTokens ?? 0,
                latencyMs, usage?.MaxTokens, usage?.RequestId, _time.GetUtcNow());

            await _recorder.RecordAsync(record, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // The usage ledger is auxiliary — never let a recording failure break an eval call.
        }
    }

    private static AiUsageStatus ParseStatus(string status) => status.ToLowerInvariant() switch
    {
        "refusal" => AiUsageStatus.Refusal,
        "error" => AiUsageStatus.Error,
        _ => AiUsageStatus.Success,
    };

    /// <summary>
    /// Turns a non-success eval-runner response into an <see cref="EvalRunnerException"/> carrying
    /// the service's own <c>detail</c> message (e.g. "…provider not configured…"), so a failed run
    /// fails loudly with the reason instead of a bare <see cref="HttpRequestException"/> → 500.
    /// </summary>
    private static async Task EnsureRunnerSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? detail = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<RunnerError>(ct);
            detail = error?.Detail;
        }
        catch
        {
            // Body was not the expected {detail} shape — fall back to the status line below.
        }

        throw new EvalRunnerException(string.IsNullOrWhiteSpace(detail)
            ? $"eval-runner: request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
            : $"eval-runner: {detail}");
    }

    // FastAPI HTTPException / our UnknownProviderError handler both serialize as {"detail": "..."}.
    private sealed record RunnerError([property: JsonPropertyName("detail")] string? Detail);

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
        [property: JsonPropertyName("fixtures")] IReadOnlyList<GeneratedDto> Fixtures,
        [property: JsonPropertyName("usage")] UsageDto? Usage);

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
        [property: JsonPropertyName("cost_usd")] decimal? CostUsd,
        [property: JsonPropertyName("usage")] UsageDto? Usage);

    private sealed record JudgeRequest(
        [property: JsonPropertyName("rubric")] string Rubric,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("output")] string Output,
        [property: JsonPropertyName("expected")] string? Expected,
        [property: JsonPropertyName("model")] string Model);

    private sealed record JudgeResponse(
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("passed")] bool Passed,
        [property: JsonPropertyName("rationale")] string Rationale,
        [property: JsonPropertyName("usage")] UsageDto? Usage);

    // The full usage block the eval-runner now returns on every model call (6.1). Present on
    // execute/judge/generate success responses; null on stub/captured paths and pre-response failures.
    private sealed record UsageDto(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens,
        [property: JsonPropertyName("cache_creation_input_tokens")] int CacheCreationInputTokens,
        [property: JsonPropertyName("cache_read_input_tokens")] int CacheReadInputTokens,
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens);
}
