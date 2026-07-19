using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Ports;
using Infrastructure.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Tests;

public class EvalRunnerClientTests
{
    [Fact]
    public void AddInfrastructure_gives_the_eval_runner_client_a_generous_timeout()
    {
        // R1 interim band-aid: a heavy synchronous run (round-debrief — Sonnet generation + Opus
        // judge per fixture) sits right at the .NET default 100s HttpClient timeout and 502s at the
        // boundary. Raise the timeout well past it (the real fix is async runs — R1). Assert the
        // configured typed client carries the generous timeout, never the 100s default.
        var services = new ServiceCollection();
        services.AddInfrastructure(
            "Host=localhost;Database=litmus;Username=u;Password=p",
            "http://eval-runner:8000");
        using var provider = services.BuildServiceProvider();

        // A typed client's logical name is TClient.Name — here IEvaluationRunner, not the impl.
        var client = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IEvaluationRunner));

        Assert.True(
            client.Timeout >= TimeSpan.FromMinutes(5),
            $"eval-runner HttpClient timeout was {client.Timeout}, expected ≥ 5 min");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            // Buffer the content so the test can inspect it after the call.
            if (request.Content is not null)
                await request.Content.LoadIntoBufferAsync();
            return respond(request);
        }
    }

    [Fact]
    public async Task Attaches_service_token_header_when_configured()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { output = "round trip" }),
        });
        // eval-runner is an internal trusted service (4.1): the DelegatingHandler stamps the shared
        // token onto every outbound request. Compose it in front of the capturing stub.
        var tokenHandler = new ServiceTokenHandler("s3cret-service-token") { InnerHandler = handler };
        var http = new HttpClient(tokenHandler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        await client.EchoAsync("round trip");

        Assert.True(handler.LastRequest!.Headers.TryGetValues("X-Service-Token", out var values));
        Assert.Equal("s3cret-service-token", Assert.Single(values!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Omits_service_token_header_when_not_configured(string? token)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { output = "round trip" }),
        });
        var tokenHandler = new ServiceTokenHandler(token) { InnerHandler = handler };
        var http = new HttpClient(tokenHandler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        await client.EchoAsync("round trip");

        Assert.False(handler.LastRequest!.Headers.Contains("X-Service-Token"));
    }

    [Fact]
    public async Task EchoAsync_posts_prompt_to_echo_and_returns_output()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { output = "round trip" }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var output = await client.EchoAsync("round trip");

        Assert.Equal("round trip", output);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/echo", handler.LastRequest.RequestUri!.AbsolutePath);
        var sent = await handler.LastRequest.Content!.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("round trip", sent!["prompt"]);
    }

    [Fact]
    public async Task GetVersionAsync_reads_the_version_endpoint()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { service = "eval-runner", version = "0.1.0", commit = "abc1234" }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var version = await client.GetVersionAsync();

        Assert.NotNull(version);
        Assert.Equal("eval-runner", version!.Service);
        Assert.Equal("0.1.0", version.Version);
        Assert.Equal("abc1234", version.Commit);
        Assert.Equal("/version", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GenerateSyntheticFixturesAsync_posts_snake_case_and_parses_snake_case_response()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                fixtures = new[]
                {
                    new { input = "generated", upstream_context = "slm-shaped", expected_output = (string?)null, seed_index = 0 },
                },
            }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var result = await client.GenerateSyntheticFixturesAsync(
            new[] { new SeedExampleData("captured input", "raw slm output", null) },
            new GenerationGuidanceData("coverage", "edges", "limits"),
            count: 1);

        var fixture = Assert.Single(result);
        Assert.Equal("generated", fixture.Input);
        Assert.Equal("slm-shaped", fixture.UpstreamContext);
        Assert.Equal(0, fixture.SeedIndex);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/generate-fixtures", handler.LastRequest.RequestUri!.AbsolutePath);

        // Wire format is snake_case — assert the exact keys the eval-runner (Pydantic) expects.
        using var sent = JsonDocument.Parse(await handler.LastRequest.Content!.ReadAsStringAsync());
        var root = sent.RootElement;
        Assert.Equal(1, root.GetProperty("count").GetInt32());
        var seed = root.GetProperty("seed_examples")[0];
        Assert.Equal("captured input", seed.GetProperty("input").GetString());
        Assert.Equal("raw slm output", seed.GetProperty("upstream_context").GetString());
        var guidance = root.GetProperty("guidance");
        Assert.Equal("coverage", guidance.GetProperty("coverage_goals").GetString());
        Assert.Equal("edges", guidance.GetProperty("edge_cases").GetString());
    }

    [Fact]
    public async Task ExecutePromptAsync_posts_snake_case_and_parses_the_execution()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                output = "the summary",
                latency_ms = 512,
                input_tokens = 1000,
                output_tokens = 500,
                cost_usd = 0.0175,
            }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var execution = await client.ExecutePromptAsync(
            "You summarize.", "claude-opus-4-8", "long article", "raw slm output");

        Assert.Equal("the summary", execution.Output);
        Assert.Equal(512, execution.LatencyMs);
        Assert.Equal(1000, execution.InputTokens);
        Assert.Equal(500, execution.OutputTokens);
        Assert.Equal(0.0175m, execution.CostUsd);
        Assert.Equal("/execute-prompt", handler.LastRequest!.RequestUri!.AbsolutePath);

        using var sent = JsonDocument.Parse(await handler.LastRequest.Content!.ReadAsStringAsync());
        var root = sent.RootElement;
        Assert.Equal("You summarize.", root.GetProperty("prompt").GetString());
        Assert.Equal("claude-opus-4-8", root.GetProperty("model").GetString());
        Assert.Equal("long article", root.GetProperty("input").GetString());
        Assert.Equal("raw slm output", root.GetProperty("upstream_context").GetString());
    }

    [Fact]
    public async Task ExecutePromptAsync_surfaces_the_eval_runner_detail_as_EvalRunnerException()
    {
        // eval-runner returns 400 {"detail": …} when a model's provider has no credentials. The
        // adapter must surface that reason (B1/B2), not swallow it into a bare HttpRequestException.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new
            {
                detail = "Provider 'anthropic' for model 'claude-opus-4-8' is not configured (missing credentials?).",
            }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var ex = await Assert.ThrowsAsync<EvalRunnerException>(() =>
            client.ExecutePromptAsync("You summarize.", "claude-opus-4-8", "input", null));

        Assert.Contains("not configured", ex.Message);
        Assert.Contains("anthropic", ex.Message);
        Assert.StartsWith("eval-runner:", ex.Message);
    }

    [Fact]
    public async Task JudgeAsync_surfaces_the_eval_runner_detail_as_EvalRunnerException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { detail = "Provider 'openai' for model 'gpt-4o' is not configured." }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var ex = await Assert.ThrowsAsync<EvalRunnerException>(() =>
            client.JudgeAsync("Is it good?", "q", "a", null, "gpt-4o"));

        Assert.Contains("not configured", ex.Message);
    }

    [Fact]
    public async Task JudgeAsync_posts_snake_case_and_parses_the_verdict()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { score = 0.8, passed = true, rationale = "accurate" }),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://eval-runner:8000") };
        var client = new EvalRunnerClient(http);

        var verdict = await client.JudgeAsync(
            "Is it correct?", "the question", "the answer", "expected", "claude-opus-4-8");

        Assert.Equal(0.8, verdict.Score);
        Assert.True(verdict.Passed);
        Assert.Equal("accurate", verdict.Rationale);
        Assert.Equal("/judge", handler.LastRequest!.RequestUri!.AbsolutePath);

        using var sent = JsonDocument.Parse(await handler.LastRequest.Content!.ReadAsStringAsync());
        var root = sent.RootElement;
        Assert.Equal("Is it correct?", root.GetProperty("rubric").GetString());
        Assert.Equal("the answer", root.GetProperty("output").GetString());
        Assert.Equal("claude-opus-4-8", root.GetProperty("model").GetString());
    }
}
