using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

// Slice 1 of 3.2: the deployed `litmus-ai` image is a single origin — the .NET API serves the
// built Angular SPA from wwwroot (no nginx). Static files + a client-route fallback are enabled
// only OUTSIDE Development, so per-process `ng serve` dev (and the compose nginx) stay in charge
// locally. Mirrors Prism/Stormboard.
public class SpaFallbackTests
{
    private static WebApplicationFactory<Program> ProductionFactoryWithSpa()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "litmus-spa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"),
            "<!doctype html><html><head><title>LitmusAI</title></head><body>litmus-spa-marker</body></html>");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseWebRoot(webRoot);
        });
    }

    [Fact]
    public async Task Outside_development_a_client_route_falls_back_to_index_html()
    {
        using var factory = ProductionFactoryWithSpa();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/prompts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("litmus-spa-marker", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Outside_development_api_and_health_routes_are_not_shadowed_by_the_spa()
    {
        using var factory = ProductionFactoryWithSpa();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task In_development_the_spa_fallback_is_not_active()
    {
        // Dev keeps the API framework-only; the SPA is served by `ng serve` / the compose nginx,
        // so an unmapped client route must 404 rather than return an app-served index.html.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/prompts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record HealthDto(string Status);
}
