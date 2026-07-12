using System.Net;
using System.Net.Http.Json;
using Infrastructure.Http;

namespace Infrastructure.Tests;

public class EvalRunnerClientTests
{
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
}
