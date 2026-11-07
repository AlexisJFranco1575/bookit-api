using Bookit.Domain.Common;
using Bookit.Domain.Notifications;
using Xunit;

namespace Bookit.Domain.Tests.Notifications;

public class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static OutboxMessage NewMessage() =>
        OutboxMessage.Create(NotificationType.AppointmentBooked, Guid.NewGuid(), Now);

    [Fact]
    public void A_new_message_is_pending_and_due_immediately()
    {
        var message = NewMessage();

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Equal(Now, message.CreatedAt);
        Assert.Equal(Now, message.NextAttemptAt);
        Assert.Null(message.ProcessedAt);
        Assert.NotEqual(Guid.Empty, message.Id);
    }

    [Fact]
    public void Rejects_a_message_without_an_appointment()
    {
        Assert.Throws<DomainException>(() =>
        {
            _ = OutboxMessage.Create(NotificationType.AppointmentBooked, Guid.Empty, Now);
        });
    }

    [Fact]
    public void Marking_a_message_as_sent_records_the_attempt()
    {
        var message = NewMessage();

        message.MarkSent(Now.AddSeconds(5));

        Assert.Equal(OutboxStatus.Sent, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(Now.AddSeconds(5), message.ProcessedAt);
        Assert.Null(message.Detail);
    }

    [Fact]
    public void A_failure_schedules_a_retry_with_growing_delays()
    {
        var message = NewMessage();

        message.MarkFailed(Now, "SMTP down");
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(Now.AddMinutes(1), message.NextAttemptAt);
        Assert.Equal("SMTP down", message.Detail);

        var second = Now.AddMinutes(1);
        message.MarkFailed(second, "SMTP down");
        Assert.Equal(second.AddMinutes(2), message.NextAttemptAt);

        var third = second.AddMinutes(2);
        message.MarkFailed(third, "SMTP down");
        Assert.Equal(third.AddMinutes(4), message.NextAttemptAt);
    }

    [Fact]
    public void After_the_last_attempt_the_message_fails_for_good()
    {
        var message = NewMessage();

        for (var attempt = 1; attempt <= OutboxMessage.MaxAttempts; attempt++)
        {
            message.MarkFailed(Now.AddHours(attempt), "SMTP down");
        }

        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Equal(OutboxMessage.MaxAttempts, message.Attempts);
        Assert.NotNull(message.ProcessedAt);
    }

    [Fact]
    public void A_very_long_error_is_truncated()
    {
        var message = NewMessage();

        message.MarkFailed(Now, new string('x', 2000));

        Assert.Equal(OutboxMessage.MaxDetailLength, message.Detail!.Length);
    }

    [Fact]
    public void Skipping_a_message_records_the_reason()
    {
        var message = NewMessage();

        message.MarkSkipped(Now, "The appointment was cancelled.");

        Assert.Equal(OutboxStatus.Skipped, message.Status);
        Assert.Equal("The appointment was cancelled.", message.Detail);
        Assert.Equal(Now, message.ProcessedAt);
    }

    [Fact]
    public void A_processed_message_cannot_be_processed_again()
    {
        var message = NewMessage();
        message.MarkSent(Now);

        Assert.Throws<DomainException>(() => message.MarkSent(Now));
        Assert.Throws<DomainException>(() => message.MarkFailed(Now, "late error"));
        Assert.Throws<DomainException>(() => message.MarkSkipped(Now, "late reason"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 16)]
    [InlineData(10, 60)]
    public void The_retry_delay_doubles_up_to_one_hour(int attempts, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), OutboxMessage.DelayAfter(attempts));
    }
}