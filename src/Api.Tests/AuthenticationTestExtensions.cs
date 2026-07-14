using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

/// <summary>
/// Shared test helper (4.1): every data endpoint now requires authentication, so tests register a
/// user (which auto-signs-in) and drive the API through the cookie-bearing client. The password
/// satisfies the identity policy. A distinct <paramref name="email"/> yields an independent user —
/// used to prove one user can't reach another's org.
/// </summary>
internal static class AuthenticationTestExtensions
{
    public const string DefaultPassword = "Correct-Horse-9";

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        this WebApplicationFactory<Program> factory, string email = "user@test.local")
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email, displayName = "Test User", password = DefaultPassword });
        res.EnsureSuccessStatusCode();
        return client;
    }
}
