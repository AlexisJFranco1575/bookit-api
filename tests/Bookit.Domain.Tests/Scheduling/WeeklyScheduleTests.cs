using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;
using Xunit;

namespace Bookit.Domain.Tests.Scheduling;

public class WeeklyScheduleTests
{
    private static WorkingPeriod Period(DayOfWeek day, int startHour, int endHour) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    [Fact]
    public void Rejects_a_period_that_ends_before_it_starts()
    {
        Assert.Throws<DomainException>(() => { _ = new WeeklySchedule([Period(DayOfWeek.Monday, 17, 9)]); });
    }

    [Fact]
    public void Rejects_overlapping_periods_on_the_same_day()
    {
        Assert.Throws<DomainException>(() =>
        {
            _ = new WeeklySchedule([Period(DayOfWeek.Monday, 9, 13), Period(DayOfWeek.Monday, 12, 17)]);
        });
    }

    [Fact]
    public void Allows_a_lunch_break_between_two_periods()
    {
        var schedule = new WeeklySchedule([Period(DayOfWeek.Monday, 9, 12), Period(DayOfWeek.Monday, 14, 17)]);

        Assert.Equal(2, schedule.Periods.Count);
    }

    [Fact]
    public void Returns_only_the_periods_of_the_requested_day()
    {
        var schedule = new WeeklySchedule([Period(DayOfWeek.Monday, 9, 12), Period(DayOfWeek.Tuesday, 9, 12)]);

        var monday = schedule.For(DayOfWeek.Monday).ToList();

        Assert.Single(monday);
        Assert.Equal(DayOfWeek.Monday, monday[0].Day);
        Assert.Empty(schedule.For(DayOfWeek.Sunday));
    }
}
