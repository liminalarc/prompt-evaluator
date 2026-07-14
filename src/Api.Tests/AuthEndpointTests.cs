using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// End-to-end coverage of the first-party auth flow (4.1): register (auto-signs-in), login, the
/// <c>/me</c> session probe, and logout — driven through the cookie the test client carries.
/// </summary>
public sealed class AuthEndpointTests : IAsyncLifetime
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

    private sealed record UserDto(Guid Id, string Email, string DisplayName);

    private static object Registration(string email) =>
        new { email, displayName = "Test User", password = "Correct-Horse-9" };

    [Fact]
    public async Task Register_signs_the_user_in_and_me_returns_them()
    {
        var client = _factory.CreateClient();

        var reg = await client.PostAsJsonAsync("/api/auth/register", Registration("ada@example.com"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var dto = await me.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("ada@example.com", dto!.Email);
        Assert.Equal("Test User", dto.DisplayName);
    }

    [Fact]
    public async Task Login_on_a_fresh_client_establishes_a_session()
    {
        // Register on one client; log in from a separate client that carries no cookie yet.
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", Registration("grace@example.com"));

        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "grace@example.com", password = "Correct-Horse-9" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401_and_no_session()
    {
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", Registration("hopper@example.com"));

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "hopper@example.com", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Me_without_a_session_returns_401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_clears_the_session()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("logout@example.com"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Register_with_a_weak_password_returns_400()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "weak@example.com", displayName = "Weak", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, reg.StatusCode);
    }
}
