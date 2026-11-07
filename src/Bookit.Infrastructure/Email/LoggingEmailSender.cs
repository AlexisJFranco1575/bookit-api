using Bookit.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Bookit.Infrastructure.Email;

/// <summary>Writes the email to the log instead of sending it. Used when Email:Mode is Log.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email not sent (Email:Mode is Log). To: {To}. Subject: {Subject}",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }
}