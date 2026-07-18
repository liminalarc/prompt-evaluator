using System.Net;
using System.Net.Http.Json;
using Application.Ports;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Auth hardening for a multi-instance deploy (3.2, re-homed from 4.1):
/// (1) a password reset invalidates live sessions immediately (SignInManager + SecurityStampValidator);
/// (2) Data-Protection keys persist to Postgres so the auth cookie is valid across replicas.
/// </summary>
public sealed class AuthHardeningTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();
    private string _conn = null!;

    private sealed class CapturingEmailSender : IEmailSender
    {
        public readonly List<EmailMessage> Sent = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Factory(string connectionString, CapturingEmailSender email) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(email);
            });
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _conn = _postgres.GetConnectionString();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

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
    public async Task Password_reset_invalidates_a_live_session()
    {
        var email = new CapturingEmailSender();
        await using var factory = new Factory(_conn, email);

        // A live, authenticated session — the cookie is held by this client.
        var live = factory.CreateClient();
        (await live.PostAsJsonAsync("/api/auth/register",
            new { email = "live@example.com", displayName = "Live", password = "Correct-Horse-9" }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await live.GetAsync("/api/auth/me")).StatusCode);

        // A password reset happens out-of-band (rotates the security stamp).
        await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = "live@example.com" });
        var (em, token) = ParseResetLink(Assert.Single(email.Sent).Body);
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password",
            new { email = em, token, newPassword = "Brand-New-Pass-9" })).EnsureSuccessStatusCode();

        // The still-held cookie is now rejected on the very next request.
        Assert.Equal(HttpStatusCode.Unauthorized, (await live.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Data_protection_keys_are_persisted_to_postgres()
    {
        var email = new CapturingEmailSender();
        await using var factory = new Factory(_conn, email);

        // Registering signs the user in, which protects the auth cookie and forces the key ring
        // to initialize + persist a key.
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "keys@example.com", displayName = "Keys", password = "Correct-Horse-9" }))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var keyCount = await db.DataProtectionKeys.CountAsync();

        Assert.True(keyCount > 0, "expected Data-Protection keys to be persisted to Postgres (multi-replica cookie validity)");
    }
}
