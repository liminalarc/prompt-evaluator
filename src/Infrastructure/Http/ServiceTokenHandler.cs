namespace Infrastructure.Http;

/// <summary>
/// Attaches the shared <c>X-Service-Token</c> header to every outbound eval-runner request.
/// eval-runner is an INTERNAL TRUSTED SERVICE (4.1): the .NET backend authenticates to it with a
/// shared service token, never user credentials. When no token is configured (dev/CI/tests) the
/// header is omitted, so the open walking-skeleton behaviour keeps working.
/// </summary>
public sealed class ServiceTokenHandler(string? serviceToken) : DelegatingHandler
{
    internal const string HeaderName = "X-Service-Token";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(serviceToken))
            request.Headers.TryAddWithoutValidation(HeaderName, serviceToken);

        return base.SendAsync(request, cancellationToken);
    }
}
