using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Spec 1.16 — the version-status &amp; backport lifecycle endpoints: mark a version "Current in source"
/// (== mark-as-backported) and read per-version status (Current / Backport-eligible / Regressed). The
/// derivation itself is unit-tested (VersionStatusTests); here we prove the endpoints, the moving
/// pointer, and the auth gate. Eligible/Regressed derive from runs (none here) so they stay false.
/// </summary>
public sealed class PromptVersionStatusEndpointTests : IAsyncLifetime
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
    private sealed record PromptDto(Guid Id, string Name, List<VersionDto> Versions, Guid? CurrentVersionId, string? CurrentVersionSha);
    private sealed record VersionStatusDto(Guid VersionId, int VersionNumber, bool IsCurrent, bool BackportEligible, bool Regressed);
    private sealed record StatusDto(Guid PromptId, Guid? CurrentVersionId, List<VersionStatusDto> Versions);

    private static async Task<Guid> CreateOrgAsync(HttpClient client)
        => (await (await client.PostAsJsonAsync("/api/organizations", new { name = "Acme" }))
            .Content.ReadFromJsonAsync<IdName>())!.Id;

    // A prompt with two versions; returns the prompt id and the two version ids.
    private static async Task<(Guid PromptId, Guid V1, Guid V2)> TwoVersionPromptAsync(HttpClient client, Guid orgId)
    {
        var prompt = (await (await client.PostAsJsonAsync($"/api/organizations/{orgId}/prompts",
            new { name = "Summarizer", description = (string?)null })).Content.ReadFromJsonAsync<PromptDto>())!;
        await client.PostAsJsonAsync($"/api/prompts/{prompt.Id}/versions",
            new { content = "v1", targetModel = "claude-sonnet-5", label = (string?)null, sourceApp = (string?)null });
        await client.PostAsJsonAsync($"/api/prompts/{prompt.Id}/versions",
            new { content = "v2", targetModel = "claude-sonnet-5", label = (string?)null, sourceApp = (string?)null });
        var full = (await client.GetFromJsonAsync<PromptDto>($"/api/prompts/{prompt.Id}"))!;
        return (prompt.Id, full.Versions[0].Id, full.Versions[1].Id);
    }

    [Fact]
    public async Task Set_current_marks_the_version_and_status_reflects_it()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var (promptId, v1, _) = await TwoVersionPromptAsync(client, orgId);

        // Before: nothing current.
        var before = (await client.GetFromJsonAsync<StatusDto>($"/api/prompts/{promptId}/version-status"))!;
        Assert.Null(before.CurrentVersionId);
        Assert.All(before.Versions, v => Assert.False(v.IsCurrent));

        var set = await client.PostAsJsonAsync($"/api/prompts/{promptId}/versions/{v1}/set-current",
            new { commitSha = "abc123" });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var status = (await set.Content.ReadFromJsonAsync<StatusDto>())!;
        Assert.Equal(v1, status.CurrentVersionId);
        Assert.True(status.Versions.Single(v => v.VersionId == v1).IsCurrent);

        // The prompt itself now carries the pointer + SHA.
        var prompt = (await client.GetFromJsonAsync<PromptDto>($"/api/prompts/{promptId}"))!;
        Assert.Equal(v1, prompt.CurrentVersionId);
        Assert.Equal("abc123", prompt.CurrentVersionSha);
    }

    [Fact]
    public async Task Set_current_moves_the_marker_forward_mark_as_backported()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var (promptId, v1, v2) = await TwoVersionPromptAsync(client, orgId);

        await client.PostAsJsonAsync($"/api/prompts/{promptId}/versions/{v1}/set-current", new { commitSha = (string?)null });
        var moved = (await (await client.PostAsJsonAsync(
            $"/api/prompts/{promptId}/versions/{v2}/set-current", new { commitSha = (string?)null }))
            .Content.ReadFromJsonAsync<StatusDto>())!;

        Assert.Equal(v2, moved.CurrentVersionId);
        Assert.True(moved.Versions.Single(v => v.VersionId == v2).IsCurrent);
        Assert.False(moved.Versions.Single(v => v.VersionId == v1).IsCurrent); // only one current
    }

    [Fact]
    public async Task Set_current_on_a_version_not_in_the_prompt_is_404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var orgId = await CreateOrgAsync(client);
        var (promptId, _, _) = await TwoVersionPromptAsync(client, orgId);

        var res = await client.PostAsJsonAsync(
            $"/api/prompts/{promptId}/versions/{Guid.NewGuid()}/set-current", new { commitSha = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_read_status_or_set_current()
    {
        var alice = await _factory.CreateAuthenticatedClientAsync("alice@test.local");
        var orgId = await CreateOrgAsync(alice);
        var (promptId, v1, _) = await TwoVersionPromptAsync(alice, orgId);

        var bob = await _factory.CreateAuthenticatedClientAsync("bob@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/prompts/{promptId}/version-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await bob.PostAsJsonAsync($"/api/prompts/{promptId}/versions/{v1}/set-current", new { commitSha = (string?)null })).StatusCode);
    }
}
