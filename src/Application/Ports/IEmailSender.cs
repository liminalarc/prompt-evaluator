namespace Application.Ports;

/// <summary>
/// Outbound transactional email (4.1) — used to deliver password-reset links. This is a seam: the
/// dev/CI default only logs. A concrete SMTP/hosted provider is wired at deployment (spec 3.2),
/// where there is an environment and credentials to run it against.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public sealed record EmailMessage(string To, string Subject, string Body);
