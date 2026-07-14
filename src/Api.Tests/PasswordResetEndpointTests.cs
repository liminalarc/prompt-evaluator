using System.Net;
using System.Net.Http.Json;
using Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Drives the forgot/reset flow (4.1) end-to-end: forgot emits a reset link (captured via a fake
/// <see cref="IEmailSender"/>), the token resets the password, the new password works and the old
/// one no longer does. Also asserts enumeration resistance for an unknown email.
/// </summary>
public sealed class PasswordResetEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private Factory _factory = null!;

    private sealed class CapturingEmailSender : IEmailSender
    {
        public readonly List<EmailMessage> Sent = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        public readonly CapturingEmailSender Email = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Email);
            });
        }
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

    private static (string email, string token) ParseResetLink(string body)
    {
        var link = body.Split('\n').First(l => l.StartsWith("http"));
        var query = new Uri(link).Query.TrimStart('?')
            .Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        return (query["email"], query["token"]);
    }

    [Fact]
    public async Task Forgot_then_reset_changes_the_password()
    {
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "reset@example.com", displayName = "Reset", password = "Correct-Horse-9" });

        var forgot = await _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password",
            new { email = "reset@example.com" });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        var (email, token) = ParseResetLink(Assert.Single(_factory.Email.Sent).Body);
        var reset = await _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = "Brand-New-Pass-9" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // The new password works; the old one no longer does.
        var withNew = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "reset@example.com", password = "Brand-New-Pass-9" });
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);

        var withOld = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "reset@example.com", password = "Correct-Horse-9" });
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);
    }

    [Fact]
    public async Task Forgot_for_an_unknown_email_returns_200_and_sends_nothing()
    {
        var forgot = await _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password",
            new { email = "ghost@example.com" });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Empty(_factory.Email.Sent);
    }

    [Fact]
    public async Task Reset_with_a_garbage_token_returns_400()
    {
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "bad-token@example.com", displayName = "Bad", password = "Correct-Horse-9" });

        var reset = await _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new { email = "bad-token@example.com", token = "not-a-real-token", newPassword = "Brand-New-Pass-9" });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }
}
