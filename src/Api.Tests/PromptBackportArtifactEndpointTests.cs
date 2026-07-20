using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Spec 1.20 — the backport-artifact endpoint. The artifact assembly (content, diff, deltas, markdown)
/// is unit-tested (BackportArtifactHandlerTests); here we prove the endpoint wiring, the no-target 404
/// (no eval runs → no backport target → no artifact), and the org auth gate.
/// </summary>
public sealed class PromptBackportArtifactEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:Postgres", connectionString);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new Factory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed record IdName(Guid Id, string Name);
    private sealed record VersionDto(Guid Id, int VersionNumber);
    private sealed record PromptDto(Guid Id, string Name, List<VersionDto> Versions);

    private static async Task<Guid> CreateOrgAsync(HttpClient client)
        => (await (await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" }))
            .Content.ReadFromJsonAsync<IdName>())!.Id;

    private static async Task<(Guid PromptId, Guid V1)> OneVersionPromptAsync(HttpClient client, Guid orgId)
    {
        var prompt = (await (await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts",
            new { name = "Summarizer", description = (string?)null })).Content.ReadFromJsonAsync<PromptDto>())!;
        await client.PostAsJsonAsync($"/api/prompts/{prompt.Id}/versions",
            new { content = "v1", targetModel = "claude-sonnet-5", label = (string?)null, sourceApp = (string?)null });
        var full = (await client.GetFromJsonAsync<PromptDto>($"/api/prompts/{prompt.Id}"))!;
        return (prompt.Id, full.Versions[0].Id);
    }

    [Fact]
    public async Task With_no_backport_target_the_artifact_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var (promptId, v1) = await OneVersionPromptAsync(client, orgId);
        await client.PostAsJsonAsync($"/api/prompts/{promptId}/versions/{v1}/set-current",
            new { commitSha = (string?)null });

        // No eval runs → nothing beats Current → no target → no artifact.
        var res = await client.GetAsync($"/api/prompts/{promptId}/backport-artifact");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_read_the_artifact()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var orgId = await CreateOrgAsync(alice);
        var (promptId, _) = await OneVersionPromptAsync(alice, orgId);

        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await bob.GetAsync($"/api/prompts/{promptId}/backport-artifact")).StatusCode);
    }
}
