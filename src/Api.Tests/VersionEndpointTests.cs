using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

// The flat GET /api/version the SPA consumes (Stormboard pattern, spec 3.3): the API's own
// version/commit/buildTime/environment/channel, distinct from the aggregated GET /version.
public class VersionEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task Get_api_version_returns_200_with_the_expected_shape()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VersionInfo>();
        Assert.NotNull(body);
        // Shape, never a literal (per the release rule) — a real semver-ish version was stamped.
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+"), body!.Version);
        Assert.False(string.IsNullOrWhiteSpace(body.Commit));
        Assert.False(string.IsNullOrWhiteSpace(body.BuildTime));
        Assert.False(string.IsNullOrWhiteSpace(body.Environment));
        Assert.False(string.IsNullOrWhiteSpace(body.Channel));
    }

    [Fact]
    public async Task Channel_defaults_to_local_when_DEPLOY_CHANNEL_is_unset()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<VersionInfo>("/api/version");

        Assert.Equal("local", body!.Channel);
    }

    [Fact]
    public async Task Channel_reflects_DEPLOY_CHANNEL_when_set()
    {
        // Proves the git-ref -> CI build-arg -> Docker ENV -> payload chain at the API layer:
        // whatever DEPLOY_CHANNEL is set to (here "prod") flows straight through.
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["DEPLOY_CHANNEL"] = "prod" })))
            .CreateClient();

        var body = await client.GetFromJsonAsync<VersionInfo>("/api/version");

        Assert.Equal("prod", body!.Channel);
    }

    private sealed record VersionInfo(
        string Version, string Commit, string BuildTime, string Environment, string Channel);
}
