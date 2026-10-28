using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;
using Xunit;

namespace Bookit.Domain.Tests.Scheduling;

public class SlotCalculatorTests
{
    // Guayaquil is UTC-5 all year, so 09:00 local is 14:00 UTC.
    private static readonly TimeZoneInfo Guayaquil = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");

    private static readonly DateOnly Monday = new(2026, 11, 2);

    private static readonly WeeklySchedule Schedule = new([
        new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
        new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(17, 0))
    ]);

    private static readonly DateTimeOffset LongAgo = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SlotCalculator _calculator = new();

    /// <summary>The UTC instant of a local time on Monday, 2 November 2026 in Guayaquil.</summary>
    private static DateTimeOffset Utc(int localHour, int minute = 0) =>
        new(2026, 11, 2, localHour + 5, minute, 0, TimeSpan.Zero);

    private static SlotQuery Query(
        TimeSpan duration,
        IReadOnlyCollection<TimeRange>? busy = null,
        DateTimeOffset? now = null,
        TimeSpan? notice = null,
        DateOnly? date = null) =>
        new(
            Schedule,
            Guayaquil,
            date ?? Monday,
            date ?? Monday,
            duration,
            TimeSpan.FromMinutes(30),
            busy ?? [],
            now ?? LongAgo,
            notice ?? TimeSpan.Zero);

    [Fact]
    public void Offers_slots_only_inside_the_working_periods()
    {
        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromMinutes(30)));

        Assert.Equal(12, slots.Count);
        Assert.Equal(Utc(9), slots[0]);
        Assert.Equal(Utc(16, 30), slots.Last());
        Assert.DoesNotContain(Utc(12), slots);
        Assert.DoesNotContain(Utc(13), slots);
    }

    [Fact]
    public void A_service_that_does_not_fit_in_any_period_has_no_slots()
    {
        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromHours(4)));

        Assert.Empty(slots);
    }

    [Fact]
    public void A_longer_service_stops_offering_starts_it_cannot_finish()
    {
        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromMinutes(60)));

        Assert.Equal(10, slots.Count);
        Assert.Contains(Utc(11), slots);
        Assert.DoesNotContain(Utc(11, 30), slots);
    }

    [Fact]
    public void An_existing_appointment_blocks_overlapping_slots_but_not_adjacent_ones()
    {
        var busy = new TimeRange(Utc(10), Utc(10, 30));

        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromMinutes(30), [busy]));

        Assert.Equal(11, slots.Count);
        Assert.DoesNotContain(Utc(10), slots);
        Assert.Contains(Utc(9, 30), slots);
        Assert.Contains(Utc(10, 30), slots);
    }

    [Fact]
    public void A_longer_service_is_blocked_by_an_appointment_inside_its_duration()
    {
        var busy = new TimeRange(Utc(10), Utc(10, 30));

        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromMinutes(60), [busy]));

        Assert.Equal(8, slots.Count);
        Assert.Contains(Utc(9), slots);
        Assert.DoesNotContain(Utc(9, 30), slots);
        Assert.DoesNotContain(Utc(10), slots);
        Assert.Contains(Utc(10, 30), slots);
    }

    [Fact]
    public void Slots_that_already_started_are_not_offered()
    {
        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromMinutes(30), now: Utc(10, 10)));

        Assert.Equal(9, slots.Count);
        Assert.Equal(Utc(10, 30), slots[0]);
    }

    [Fact]
    public void The_minimum_notice_hides_slots_that_start_too_soon()
    {
        var slots = _calculator.GetAvailableStarts(
            Query(TimeSpan.FromMinutes(30), now: Utc(8), notice: TimeSpan.FromHours(2)));

        Assert.Equal(10, slots.Count);
        Assert.Equal(Utc(10), slots[0]);
    }

    [Fact]
    public void A_day_without_working_hours_has_no_slots()
    {
        var sunday = new DateOnly(2026, 11, 1);

        var slots = _calculator.GetAvailableStarts(Query(TimeSpan.FromMinutes(30), date: sunday));

        Assert.Empty(slots);
    }

    [Fact]
    public void Honours_daylight_saving_when_clocks_spring_forward()
    {
        // On 8 March 2026 New York jumps from 02:00 to 03:00, so 01:00-04:00 local is only two real hours.
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var schedule = new WeeklySchedule([
            new WorkingPeriod(DayOfWeek.Sunday, new TimeOnly(1, 0), new TimeOnly(4, 0))
        ]);
        var day = new DateOnly(2026, 3, 8);
        var now = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var slots = _calculator.GetAvailableStarts(new SlotQuery(
            schedule, newYork, day, day, TimeSpan.FromHours(1), TimeSpan.FromHours(1), [], now, TimeSpan.Zero));

        Assert.Equal(2, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 6, 0, 0, TimeSpan.Zero), slots[0]);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), slots[1]);
    }

    [Fact]
    public void Rejects_a_date_range_longer_than_the_maximum()
    {
        var query = new SlotQuery(
            Schedule, Guayaquil, Monday, Monday.AddDays(SlotCalculator.MaxDays),
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30), [], LongAgo, TimeSpan.Zero);

        Assert.Throws<DomainException>(() => { _ = _calculator.GetAvailableStarts(query); });
    }

    [Fact]
    public void Rejects_a_service_duration_that_is_not_positive()
    {
        var query = Query(TimeSpan.Zero);

        Assert.Throws<DomainException>(() => { _ = _calculator.GetAvailableStarts(query); });
    }
}
