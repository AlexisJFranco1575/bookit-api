using Bookit.Domain.Common;

namespace Bookit.Domain.Scheduling;

/// <summary>Everything the calculator needs to decide which start times can be booked.</summary>
/// <param name="Schedule">The provider's recurring weekly hours, in local time.</param>
/// <param name="TimeZone">The business's time zone; working hours are read in this zone.</param>
/// <param name="FromDate">First local date to consider (inclusive).</param>
/// <param name="ToDate">Last local date to consider (inclusive).</param>
/// <param name="ServiceDuration">How long the requested service takes.</param>
/// <param name="Step">Distance between offered start times (for example 15 or 30 minutes).</param>
/// <param name="Busy">Time already taken by other appointments, in UTC.</param>
/// <param name="Now">The current instant.</param>
/// <param name="MinimumNotice">How far ahead of Now a slot must start to be offered.</param>
public sealed record SlotQuery(
    WeeklySchedule Schedule,
    TimeZoneInfo TimeZone,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeSpan ServiceDuration,
    TimeSpan Step,
    IReadOnlyCollection<TimeRange> Busy,
    DateTimeOffset Now,
    TimeSpan MinimumNotice);

/// <summary>Pure domain service: no database, no clock, no I/O. Everything it needs comes in the query.</summary>
public sealed class SlotCalculator
{
    public const int MaxDays = 62;

    /// <summary>Returns the UTC start time of every bookable slot, in chronological order.</summary>
    public IReadOnlyList<DateTimeOffset> GetAvailableStarts(SlotQuery query)
    {
        Validate(query);

        var slots = new List<DateTimeOffset>();
        var earliest = query.Now + query.MinimumNotice;

        for (var date = query.FromDate; date <= query.ToDate; date = date.AddDays(1))
        {
            foreach (var period in query.Schedule.For(date.DayOfWeek))
            {
                var periodStart = ToUtc(date, period.Start, query.TimeZone);
                var periodEnd = ToUtc(date, period.End, query.TimeZone);

                for (var start = periodStart; start + query.ServiceDuration <= periodEnd; start += query.Step)
                {
                    if (start < earliest)
                    {
                        continue;
                    }

                    var candidate = new TimeRange(start, start + query.ServiceDuration);

                    if (query.Busy.Any(busy => busy.Overlaps(candidate)))
                    {
                        continue;
                    }

                    slots.Add(start);
                }
            }
        }

        return slots;
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        // The local time has no zone attached; the zone decides its UTC offset for that exact date,
        // so daylight-saving changes are honoured.
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static void Validate(SlotQuery query)
    {
        if (query.ServiceDuration <= TimeSpan.Zero)
        {
            throw new DomainException("The service duration must be positive.");
        }

        if (query.Step <= TimeSpan.Zero)
        {
            throw new DomainException("The slot step must be positive.");
        }

        if (query.MinimumNotice < TimeSpan.Zero)
        {
            throw new DomainException("The minimum notice cannot be negative.");
        }

        if (query.ToDate < query.FromDate)
        {
            throw new DomainException("The end date cannot be before the start date.");
        }

        if (query.ToDate.DayNumber - query.FromDate.DayNumber + 1 > MaxDays)
        {
            throw new DomainException($"You can look at most {MaxDays} days ahead at a time.");
        }
    }
}
