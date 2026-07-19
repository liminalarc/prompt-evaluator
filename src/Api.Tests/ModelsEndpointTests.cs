using System.Net;
using System.Net.Http.Json;
using Application;
using Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public sealed class ModelsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:Postgres", connectionString);
    }

    // Boots the app with a stubbed eval-runner so availability can be asserted deterministically
    // (the real HTTP client has no eval-runner to reach in tests).
    private sealed class FactoryWithRunner(string connectionString, IEvaluationRunner runner)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEvaluationRunner>();
                services.AddScoped(_ => runner);
            });
        }
    }

    // Only GetConfiguredProvidersAsync is exercised here; the rest are not called.
    private sealed class FakeRunner(IReadOnlyList<string>? providers) : IEvaluationRunner
    {
        public Task<IReadOnlyList<string>?> GetConfiguredProvidersAsync(CancellationToken ct = default)
            => Task.FromResult(providers);

        public Task<string> EchoAsync(string prompt, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ServiceVersion?> GetVersionAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<GeneratedFixtureData>> GenerateSyntheticFixturesAsync(
            IReadOnlyList<SeedExampleData> seeds, GenerationGuidanceData guidance, int count, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<PromptExecution> ExecutePromptAsync(
            string promptContent, string targetModel, string input, string? upstreamContext, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<JudgeVerdict> JudgeAsync(
            string rubric, string input, string output, string? expected, string judgeModel, CancellationToken ct = default)
            => throw new NotSupportedException();
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

    private sealed record ModelDto(
        Guid Id, string ModelId, string DisplayName, string Provider,
        List<string> Roles, decimal? InputPricePerMTokUsd, decimal? OutputPricePerMTokUsd,
        bool IsActive, bool Available);

    [Fact]
    public async Task Get_returns_the_seeded_catalog_with_id_name_provider_and_roles()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        Assert.NotNull(models);
        var opus = Assert.Single(models!, m => m.ModelId == "claude-opus-4-8");
        Assert.Equal("Claude Opus 4.8", opus.DisplayName);
        Assert.Equal("Anthropic", opus.Provider);
        Assert.Equal(new[] { "subject", "judge", "generator" }, opus.Roles);
        Assert.Equal(5m, opus.InputPricePerMTokUsd);
        Assert.Equal(25m, opus.OutputPricePerMTokUsd);
        Assert.True(opus.IsActive);

        // All eight seeded models are present, across both providers.
        var ids = models!.Select(m => m.ModelId).ToList();
        Assert.Contains("claude-sonnet-5", ids);
        Assert.Contains("claude-haiku-4-5", ids);
        Assert.Contains("gpt-4o", ids);
        Assert.Contains("gpt-4o-mini", ids);

        // 1.19 — current Anthropic models added for onboarding fidelity (Golf runs Sonnet 4.6).
        Assert.Contains("claude-sonnet-4-6", ids);
        Assert.Contains("claude-opus-4-7", ids);
        Assert.Contains("claude-opus-4-6", ids);

        // Sonnet 4.6 is Anthropic, all roles, active, priced.
        var sonnet46 = Assert.Single(models!, m => m.ModelId == "claude-sonnet-4-6");
        Assert.Equal("Claude Sonnet 4.6", sonnet46.DisplayName);
        Assert.Equal("Anthropic", sonnet46.Provider);
        Assert.Equal(new[] { "subject", "judge", "generator" }, sonnet46.Roles);
        Assert.Equal(3m, sonnet46.InputPricePerMTokUsd);
        Assert.Equal(15m, sonnet46.OutputPricePerMTokUsd);
        Assert.True(sonnet46.IsActive);
    }

    [Fact]
    public async Task Get_offers_a_non_claude_model_with_null_pricing()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        var mini = Assert.Single(models!, m => m.ModelId == "gpt-4o-mini");
        Assert.Equal("OpenAi", mini.Provider);
        Assert.Null(mini.InputPricePerMTokUsd);
        Assert.Null(mini.OutputPricePerMTokUsd);
    }

    [Fact]
    public async Task Get_requires_authentication()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/models");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Get_marks_a_model_unavailable_when_its_provider_has_no_configured_credentials()
    {
        // The eval-runner reports only Anthropic configured — OpenAI models are unavailable.
        using var factory = new FactoryWithRunner(_postgres.GetConnectionString(), new FakeRunner(new[] { "anthropic" }));
        var client = await factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        Assert.True(Assert.Single(models!, m => m.ModelId == "claude-opus-4-8").Available);
        Assert.False(Assert.Single(models!, m => m.ModelId == "gpt-4o").Available);
    }

    [Fact]
    public async Task Get_treats_every_model_available_when_the_eval_runner_is_unreachable()
    {
        // Null configured-providers = unknown; models must not be hidden.
        using var factory = new FactoryWithRunner(_postgres.GetConnectionString(), new FakeRunner(null));
        var client = await factory.CreateAuthenticatedClientAsync();

        var models = await client.GetFromJsonAsync<List<ModelDto>>("/api/models");

        Assert.All(models!, m => Assert.True(m.Available));
    }

    // ---- Admin management (AC 5) ----

    private const string AdminEmail = "admin@test.local";

    // Boots the app configured to seed a bootstrap admin, so a global admin exists to log in as.
    private sealed class AdminFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.UseSetting("Auth:BootstrapAdmin:Email", AdminEmail);
            builder.UseSetting("Auth:BootstrapAdmin:Password", AuthenticationTestExtensions.DefaultPassword);
            builder.UseSetting("Auth:BootstrapAdmin:DisplayName", "Admin");
        }
    }

    private sealed record AuthUserDto(Guid Id, string Email, string DisplayName, bool IsAdmin);

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = AuthenticationTestExtensions.DefaultPassword });
        res.EnsureSuccessStatusCode();
        return client;
    }

    private static object NewModelBody(string modelId) => new
    {
        modelId,
        displayName = "Test Model",
        provider = "Anthropic",
        roles = new[] { "subject", "judge" },
        inputPricePerMTokUsd = (decimal?)null,
        outputPricePerMTokUsd = (decimal?)null,
    };

    [Fact]
    public async Task Admin_can_create_edit_and_deactivate_an_entry_that_drives_the_droplist()
    {
        using var factory = new AdminFactory(_postgres.GetConnectionString());
        var admin = await LoginAsync(factory, AdminEmail);

        // Create — appears in the active droplist.
        var create = await admin.PostAsJsonAsync("/api/models", NewModelBody("claude-test-x"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ModelDto>();
        var active = await admin.GetFromJsonAsync<List<ModelDto>>("/api/models");
        Assert.Contains(active!, m => m.ModelId == "claude-test-x");

        // Edit — the change persists.
        var update = await admin.PutAsJsonAsync($"/api/models/{created!.Id}", new
        {
            displayName = "Renamed",
            provider = "Anthropic",
            roles = new[] { "judge" },
            inputPricePerMTokUsd = (decimal?)2m,
            outputPricePerMTokUsd = (decimal?)8m,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // Deactivate — drops out of the droplist, but the admin sees it via includeInactive.
        var deactivate = await admin.PostAsync($"/api/models/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var afterActive = await admin.GetFromJsonAsync<List<ModelDto>>("/api/models");
        Assert.DoesNotContain(afterActive!, m => m.ModelId == "claude-test-x");
        var all = await admin.GetFromJsonAsync<List<ModelDto>>("/api/models?includeInactive=true");
        var reloaded = Assert.Single(all!, m => m.ModelId == "claude-test-x");
        Assert.False(reloaded.IsActive);
        Assert.Equal("Renamed", reloaded.DisplayName);
    }

    [Fact]
    public async Task Create_with_a_duplicate_model_id_returns_400()
    {
        using var factory = new AdminFactory(_postgres.GetConnectionString());
        var admin = await LoginAsync(factory, AdminEmail);

        // claude-opus-4-8 is already seeded.
        var res = await admin.PostAsJsonAsync("/api/models", NewModelBody("claude-opus-4-8"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_manage_the_catalog()
    {
        var member = await _factory.CreateAuthenticatedClientAsync("member@test.local");

        var create = await member.PostAsJsonAsync("/api/models", NewModelBody("gpt-new"));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var includeInactive = await member.GetAsync("/api/models?includeInactive=true");
        Assert.Equal(HttpStatusCode.Forbidden, includeInactive.StatusCode);
    }

    [Fact]
    public async Task Login_and_me_report_admin_status()
    {
        using var factory = new AdminFactory(_postgres.GetConnectionString());

        var adminLogin = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = AdminEmail, password = AuthenticationTestExtensions.DefaultPassword });
        var admin = await adminLogin.Content.ReadFromJsonAsync<AuthUserDto>();
        Assert.True(admin!.IsAdmin);

        var member = await factory.CreateAuthenticatedClientAsync("plain@test.local");
        var me = await member.GetFromJsonAsync<AuthUserDto>("/api/auth/me");
        Assert.False(me!.IsAdmin);
    }
}
