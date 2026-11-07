using Bookit.Application.Notifications;
using Bookit.Domain.Appointments;
using Bookit.Domain.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class OutboxStore(BookitDbContext db) : IOutboxStore
{
    // SQL Server error numbers for "duplicate key" violations (unique index / unique constraint).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const int ReminderBatchSize = 200;

    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await db.OutboxMessages
            .Where(message => message.Status == OutboxStatus.Pending && message.NextAttemptAt <= now)
            .OrderBy(message => message.NextAttemptAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<int> EnqueueRemindersAsync(
        DateTimeOffset now,
        TimeSpan leadTime,
        CancellationToken cancellationToken)
    {
        var horizon = now + leadTime;

        // Booked appointments that start inside the window and have no reminder row yet (not even a skipped one).
        var candidates = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.Status == AppointmentStatus.Booked
                                  && appointment.StartsAt > now
                                  && appointment.StartsAt <= horizon
                                  && !db.OutboxMessages.Any(message =>
                                      message.AppointmentId == appointment.Id
                                      && message.Type == NotificationType.AppointmentReminder))
            .OrderBy(appointment => appointment.StartsAt)
            .Select(appointment => new { appointment.Id, appointment.CreatedAt, appointment.StartsAt })
            .Take(ReminderBatchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        foreach (var candidate in candidates)
        {
            var reminder = OutboxMessage.Create(NotificationType.AppointmentReminder, candidate.Id, now);

            // Booked less than a lead time ahead: the confirmation email was reminder enough. The skipped row
            // keeps the same appointment from being looked at again on every cycle.
            if (candidate.StartsAt - candidate.CreatedAt <= leadTime)
            {
                reminder.MarkSkipped(now, "Booked inside the reminder window; the confirmation was enough.");
            }

            db.OutboxMessages.Add(reminder);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return candidates.Count;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Another process planned the same reminders first. Nothing is lost: it created them.
            var pending = db.ChangeTracker.Entries<OutboxMessage>()
                .Where(entry => entry.State == EntityState.Added)
                .ToList();

            foreach (var entry in pending)
            {
                entry.State = EntityState.Detached;
            }

            return 0;
        }
    }
}