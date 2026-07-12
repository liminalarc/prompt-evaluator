using System.Net.Http.Json;
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

    // Serialized with System.Text.Json web defaults (camelCase) -> {"prompt":...} / {"output":...}.
    private sealed record EchoRequest(string Prompt);

    private sealed record EchoResponse(string Output);

    private sealed record VersionDto(string Service, string Version, string Commit);
}
