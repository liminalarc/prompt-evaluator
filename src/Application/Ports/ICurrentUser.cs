namespace Application.Ports;

/// <summary>
/// The authenticated caller for the current request (4.1), resolved from the auth cookie's claims.
/// An Api adapter over <c>IHttpContextAccessor</c> implements it. Application use cases read this to
/// scope work to the caller; unauthenticated requests report <see cref="IsAuthenticated"/> false and
/// a null <see cref="UserId"/>.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }
}
