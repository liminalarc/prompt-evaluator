using Application.Ports;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// The default <see cref="IEmailSender"/> for dev/CI: it logs the message instead of sending it,
/// so the password-reset flow is fully exercisable without an email provider. A real SMTP/hosted
/// sender replaces this at deployment (spec 3.2) via configuration — one <see cref="IEmailSender"/>
/// code path, swapped at the composition root.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Email suppressed (no provider configured). To={To} Subject={Subject}\n{Body}",
            message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
