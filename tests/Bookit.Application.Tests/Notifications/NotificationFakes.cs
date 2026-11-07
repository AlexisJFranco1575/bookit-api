using Bookit.Application.Notifications;
using Bookit.Domain.Notifications;

namespace Bookit.Application.Tests;

/// <summary>A clock that tests can move forward, to watch retry delays pass.</summary>
internal sealed class MovableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class FakeOutboxStore : IOutboxStore
{
    public List<OutboxMessage> Messages { get; } = [];

    public int SaveCount { get; private set; }

    /// <summary>What the fake reports as "reminders created".</summary>
    public int RemindersToReport { get; set; }

    public DateTimeOffset? LastReminderNow { get; private set; }

    public TimeSpan? LastReminderLead { get; private set; }

    public Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OutboxMessage> due = Messages
            .Where(message => message.Status == OutboxStatus.Pending && message.NextAttemptAt <= now)
            .OrderBy(message => message.NextAttemptAt)
            .Take(batchSize)
            .ToList();

        return Task.FromResult(due);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<int> EnqueueRemindersAsync(DateTimeOffset now, TimeSpan leadTime, CancellationToken cancellationToken)
    {
        LastReminderNow = now;
        LastReminderLead = leadTime;

        return Task.FromResult(RemindersToReport);
    }
}

internal sealed class FakeNotificationDataReader : INotificationDataReader
{
    public Dictionary<Guid, NotificationData> Items { get; } = [];

    public Task<NotificationData?> GetAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.GetValueOrDefault(appointmentId));
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>When set, every send throws it.</summary>
    public Exception? FailWith { get; set; }

    /// <summary>When set, sends to this address fail and all others succeed.</summary>
    public string? FailForRecipient { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        if (FailForRecipient == message.To)
        {
            throw new InvalidOperationException("Mailbox unavailable.");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}