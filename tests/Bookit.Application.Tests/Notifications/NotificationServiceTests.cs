#pragma warning disable xUnit1051

using Bookit.Application.Notifications;
using Bookit.Domain.Appointments;
using Bookit.Domain.Notifications;
using Xunit;

namespace Bookit.Application.Tests;

public class NotificationServiceTests
{
    private static readonly DateTimeOffset Start = BookingWorld.Now;

    private readonly FakeOutboxStore _outbox = new();
    private readonly FakeNotificationDataReader _data = new();
    private readonly FakeEmailSender _email = new();
    private readonly MovableTimeProvider _clock = new(Start);
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(_outbox, _data, _email, _clock);
    }

    private static NotificationData Details(
        AppointmentStatus status = AppointmentStatus.Booked,
        string email = "ana@example.com") =>
        new(
            Guid.NewGuid(),
            status,
            "Ana Perez",
            email,
            new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 2, 14, 30, 0, TimeSpan.Zero),
            "Haircut",
            "Carlos",
            "Barber Shop",
            "America/Guayaquil");

    private OutboxMessage Enqueue(NotificationType type, NotificationData details, DateTimeOffset? dueAt = null)
    {
        _data.Items[details.AppointmentId] = details;

        var message = OutboxMessage.Create(type, details.AppointmentId, dueAt ?? Start);
        _outbox.Messages.Add(message);

        return message;
    }

    [Fact]
    public async Task Sends_the_confirmation_and_marks_the_message_sent()
    {
        var message = Enqueue(NotificationType.AppointmentBooked, Details());

        var result = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(1, 0, 0), result);
        var email = Assert.Single(_email.Sent);
        Assert.Equal("ana@example.com", email.To);
        Assert.StartsWith("Appointment confirmed:", email.Subject);
        Assert.Equal(OutboxStatus.Sent, message.Status);
        Assert.Equal(1, _outbox.SaveCount);
    }

    [Fact]
    public async Task A_failed_send_is_retried_later_and_delivered_once_the_mail_server_is_back()
    {
        var message = Enqueue(NotificationType.AppointmentBooked, Details());
        _email.FailWith = new InvalidOperationException("SMTP down");

        var first = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(0, 1, 0), first);
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("SMTP down", message.Detail);
        Assert.Equal(Start.AddMinutes(1), message.NextAttemptAt);

        // Right away, nothing is due: the retry waits for its delay.
        var tooSoon = await _service.ProcessDueAsync(10, CancellationToken.None);
        Assert.Equal(new ProcessingResult(0, 0, 0), tooSoon);

        _clock.Advance(TimeSpan.FromMinutes(2));
        _email.FailWith = null;

        var second = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(1, 0, 0), second);
        Assert.Equal(OutboxStatus.Sent, message.Status);
        Assert.Equal(2, message.Attempts);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task The_message_is_given_up_after_the_last_attempt()
    {
        var message = Enqueue(NotificationType.AppointmentBooked, Details());
        _email.FailWith = new InvalidOperationException("SMTP down");

        for (var attempt = 0; attempt < OutboxMessage.MaxAttempts; attempt++)
        {
            await _service.ProcessDueAsync(10, CancellationToken.None);
            _clock.Advance(TimeSpan.FromHours(2));
        }

        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Equal(OutboxMessage.MaxAttempts, message.Attempts);
        Assert.Empty(_email.Sent);

        // A failed message is never picked up again.
        var after = await _service.ProcessDueAsync(10, CancellationToken.None);
        Assert.Equal(new ProcessingResult(0, 0, 0), after);
    }

    [Fact]
    public async Task A_reminder_for_a_cancelled_appointment_is_skipped()
    {
        var message = Enqueue(NotificationType.AppointmentReminder, Details(AppointmentStatus.Cancelled));

        var result = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(0, 0, 1), result);
        Assert.Equal(OutboxStatus.Skipped, message.Status);
        Assert.Contains("Cancelled", message.Detail);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task A_message_whose_appointment_is_missing_is_skipped()
    {
        var message = OutboxMessage.Create(NotificationType.AppointmentBooked, Guid.NewGuid(), Start);
        _outbox.Messages.Add(message);

        var result = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(0, 0, 1), result);
        Assert.Equal(OutboxStatus.Skipped, message.Status);
    }

    [Fact]
    public async Task The_cancellation_notice_is_sent_for_a_cancelled_appointment()
    {
        Enqueue(NotificationType.AppointmentCancelled, Details(AppointmentStatus.Cancelled));

        var result = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(1, 0, 0), result);
        Assert.StartsWith("Appointment cancelled:", Assert.Single(_email.Sent).Subject);
    }

    [Fact]
    public async Task One_failing_message_does_not_stop_the_others()
    {
        var broken = Enqueue(NotificationType.AppointmentBooked, Details(email: "broken@example.com"));
        var healthy = Enqueue(NotificationType.AppointmentBooked, Details(email: "fine@example.com"));
        _email.FailForRecipient = "broken@example.com";

        var result = await _service.ProcessDueAsync(10, CancellationToken.None);

        Assert.Equal(new ProcessingResult(1, 1, 0), result);
        Assert.Equal(OutboxStatus.Pending, broken.Status);
        Assert.Equal(OutboxStatus.Sent, healthy.Status);
        Assert.Equal("fine@example.com", Assert.Single(_email.Sent).To);
    }

    [Fact]
    public async Task Only_due_messages_are_processed_and_the_batch_size_is_respected()
    {
        Enqueue(NotificationType.AppointmentBooked, Details(email: "a@example.com"));
        Enqueue(NotificationType.AppointmentBooked, Details(email: "b@example.com"));
        Enqueue(NotificationType.AppointmentBooked, Details(email: "c@example.com"));
        var future = Enqueue(NotificationType.AppointmentBooked, Details(email: "later@example.com"), Start.AddHours(1));

        var result = await _service.ProcessDueAsync(2, CancellationToken.None);

        Assert.Equal(new ProcessingResult(2, 0, 0), result);
        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(2, _outbox.Messages.Count(message => message.Status == OutboxStatus.Pending));
        Assert.Equal(OutboxStatus.Pending, future.Status);
        Assert.Equal(0, future.Attempts);
    }

    [Fact]
    public async Task Scheduling_reminders_asks_the_store_for_a_24_hour_window_from_now()
    {
        _outbox.RemindersToReport = 4;

        var created = await _service.ScheduleRemindersAsync(CancellationToken.None);

        Assert.Equal(4, created);
        Assert.Equal(Start, _outbox.LastReminderNow);
        Assert.Equal(TimeSpan.FromHours(24), _outbox.LastReminderLead);
    }
}