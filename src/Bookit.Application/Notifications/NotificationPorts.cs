using Bookit.Domain.Appointments;
using Bookit.Domain.Notifications;

namespace Bookit.Application.Notifications;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>Delivers an email. Throws if delivery fails, so the message stays in the outbox for a retry.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Everything needed to write an email about an appointment, read from the database at send time.</summary>
public sealed record NotificationData(
    Guid AppointmentId,
    AppointmentStatus Status,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string ServiceName,
    string ProviderName,
    string BusinessName,
    string TimeZoneId);

public interface INotificationDataReader
{
    Task<NotificationData?> GetAsync(Guid appointmentId, CancellationToken cancellationToken);
}

public interface IOutboxStore
{
    /// <summary>Pending messages whose time has come, oldest first, loaded for update.</summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the reminder message of every booked appointment that starts within the lead time and has none yet.
    /// Returns how many were created. Safe to call from several processes at once.
    /// </summary>
    Task<int> EnqueueRemindersAsync(DateTimeOffset now, TimeSpan leadTime, CancellationToken cancellationToken);
}