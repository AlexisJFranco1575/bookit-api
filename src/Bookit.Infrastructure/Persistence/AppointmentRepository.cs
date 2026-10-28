using System.Data;
using Bookit.Application.Booking;
using Bookit.Application.Common;
using Bookit.Domain.Appointments;
using Bookit.Domain.Scheduling;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class AppointmentRepository(BookitDbContext db) : IAppointmentRepository
{
    // SQL Server error numbers for "duplicate key" violations (unique index / unique constraint).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<TimeRange>> GetBusyRangesAsync(
        Guid providerId,
        TimeRange window,
        CancellationToken cancellationToken)
    {
        var windowStart = window.Start;
        var windowEnd = window.End;

        var rows = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.ProviderId == providerId
                                  && appointment.Status == AppointmentStatus.Booked
                                  && appointment.StartsAt < windowEnd
                                  && appointment.EndsAt > windowStart)
            .Select(appointment => new { appointment.StartsAt, appointment.EndsAt })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new TimeRange(row.StartsAt.ToUniversalTime(), row.EndsAt.ToUniversalTime()))
            .ToList();
    }

    public async Task<bool> TryAddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Only one request at a time may book a given provider. The lock lives until the transaction
            // ends, so the overlap check below and the insert are one indivisible step for that provider.
            await AcquireProviderLockAsync(appointment.ProviderId, cancellationToken);

            var startsAt = appointment.StartsAt;
            var endsAt = appointment.EndsAt;

            var overlaps = await db.Appointments.AnyAsync(
                other => other.ProviderId == appointment.ProviderId
                         && other.Status == AppointmentStatus.Booked
                         && other.StartsAt < endsAt
                         && other.EndsAt > startsAt,
                cancellationToken);

            if (overlaps)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            db.Appointments.Add(appointment);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // The unique index caught a duplicate the lock should already have prevented. Treat it as taken.
            db.Entry(appointment).State = EntityState.Detached;
            return false;
        }
    }

    public Task<Appointment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Appointments.FirstOrDefaultAsync(appointment => appointment.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private async Task AcquireProviderLockAsync(Guid providerId, CancellationToken cancellationToken)
    {
        // An application lock is held by name, not by row, and is released automatically when the
        // transaction commits or rolls back. Status 0 or 1 means granted; negative means it was not.
        var status = new SqlParameter("@status", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255)
        {
            Value = $"bookit:provider:{providerId:N}"
        };
        var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = (int)LockTimeout.TotalMilliseconds };

        await db.Database.ExecuteSqlRawAsync(
            "EXEC @status = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', "
            + "@LockOwner = 'Transaction', @LockTimeout = @timeout",
            new object[] { status, resource, timeout },
            cancellationToken);

        if (status.Value is not int result || result < 0)
        {
            throw new ConflictException("The calendar is busy right now, please try again.");
        }
    }
}
