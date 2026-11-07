using Bookit.Domain.Common;

namespace Bookit.Domain.Notifications;

public enum NotificationType
{
    AppointmentBooked,
    AppointmentCancelled,
    AppointmentReminder
}

public enum OutboxStatus
{
    /// <summary>Waiting to be sent, now or after a retry delay.</summary>
    Pending,

    Sent,

    /// <summary>Gave up after the maximum number of attempts.</summary>
    Failed,

    /// <summary>Deliberately not sent (for example the appointment was cancelled in the meantime).</summary>
    Skipped
}

/// <summary>
/// A notification that must be delivered. It is saved in the same transaction as the change that caused it,
/// so a mail server outage can never lose it; a background worker delivers it later and retries on failure.
/// The message holds only what happened and to which appointment: the email text is written at send time.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxAttempts = 6;
    public const int MaxDetailLength = 500;

    private static readonly TimeSpan MaxDelay = TimeSpan.FromHours(1);

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid AppointmentId { get; private set; }

    public OutboxStatus Status { get; private set; }

    /// <summary>How many times delivery has been tried.</summary>
    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The message is not delivered before this instant (it moves forward after each failure).</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>The last error, or the reason the message was skipped.</summary>
    public string? Detail { get; private set; }

    public static OutboxMessage Create(NotificationType type, Guid appointmentId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("The notification type is not valid.");
        }

        if (appointmentId == Guid.Empty)
        {
            throw new DomainException("A notification needs an appointment.");
        }

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            AppointmentId = appointmentId,
            Status = OutboxStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now
        };
    }

    /// <summary>The wait before the next try: 1, 2, 4, 8, 16... minutes, never more than an hour.</summary>
    public static TimeSpan DelayAfter(int attempts)
    {
        var delay = TimeSpan.FromMinutes(Math.Pow(2, Math.Max(attempts, 1) - 1));

        return delay > MaxDelay ? MaxDelay : delay;
    }

    public void MarkSent(DateTimeOffset now)
    {
        RequirePending();

        Attempts++;
        Status = OutboxStatus.Sent;
        ProcessedAt = now;
        Detail = null;
    }

    public void MarkFailed(DateTimeOffset now, string? error)
    {
        RequirePending();

        Attempts++;
        Detail = Truncate(string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim());

        if (Attempts >= MaxAttempts)
        {
            Status = OutboxStatus.Failed;
            ProcessedAt = now;
            return;
        }

        NextAttemptAt = now + DelayAfter(Attempts);
    }

    public void MarkSkipped(DateTimeOffset now, string reason)
    {
        RequirePending();

        Status = OutboxStatus.Skipped;
        ProcessedAt = now;
        Detail = Truncate(reason);
    }

    private void RequirePending()
    {
        if (Status != OutboxStatus.Pending)
        {
            throw new DomainException($"The message was already processed (status: {Status}).");
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxDetailLength ? value : value[..MaxDetailLength];
}