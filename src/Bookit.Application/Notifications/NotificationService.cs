using Bookit.Domain.Appointments;
using Bookit.Domain.Notifications;

namespace Bookit.Application.Notifications;

/// <param name="Sent">Delivered.</param>
/// <param name="Failed">Failed this time (it will be retried, or given up on after the last attempt).</param>
/// <param name="Skipped">Deliberately not sent.</param>
public sealed record ProcessingResult(int Sent, int Failed, int Skipped);

/// <summary>Delivers the pending messages of the outbox and plans the reminders.</summary>
public sealed class NotificationService(
    IOutboxStore outbox,
    INotificationDataReader data,
    IEmailSender email,
    TimeProvider clock)
{
    public static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(24);

    public Task<int> ScheduleRemindersAsync(CancellationToken cancellationToken) =>
        outbox.EnqueueRemindersAsync(clock.GetUtcNow(), ReminderLeadTime, cancellationToken);

    public async Task<ProcessingResult> ProcessDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        var due = await outbox.ClaimDueAsync(clock.GetUtcNow(), batchSize, cancellationToken);

        var sent = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var message in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessOneAsync(message, cancellationToken);

            // Saved one by one: if the process stops halfway, what was already sent is not sent again.
            await outbox.SaveChangesAsync(cancellationToken);

            switch (message.Status)
            {
                case OutboxStatus.Sent:
                    sent++;
                    break;
                case OutboxStatus.Skipped:
                    skipped++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        return new ProcessingResult(sent, failed, skipped);
    }

    private async Task ProcessOneAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var details = await data.GetAsync(message.AppointmentId, cancellationToken);

            if (details is null)
            {
                message.MarkSkipped(clock.GetUtcNow(), "The appointment no longer exists.");
                return;
            }

            // The email must still be true when it arrives: a cancellation notice only for a cancelled
            // appointment, and a confirmation or reminder only for one that is still booked.
            var stillApplies = message.Type == NotificationType.AppointmentCancelled
                ? details.Status == AppointmentStatus.Cancelled
                : details.Status == AppointmentStatus.Booked;

            if (!stillApplies)
            {
                message.MarkSkipped(clock.GetUtcNow(), $"Not sent: the appointment is {details.Status}.");
                return;
            }

            await email.SendAsync(EmailComposer.Compose(message.Type, details), cancellationToken);

            message.MarkSent(clock.GetUtcNow());
        }
        catch (OperationCanceledException)
        {
            // Shutting down: leave the message untouched so it is picked up again.
            throw;
        }
        catch (Exception exception)
        {
            message.MarkFailed(clock.GetUtcNow(), exception.Message);
        }
    }
}