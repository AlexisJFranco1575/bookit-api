using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;
using Xunit;

namespace Bookit.Domain.Tests.Scheduling;

public class TimeRangeTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rejects_an_empty_range()
    {
        Assert.Throws<DomainException>(() => { _ = new TimeRange(Start, Start); });
    }

    [Fact]
    public void Rejects_a_range_that_ends_before_it_starts()
    {
        Assert.Throws<DomainException>(() => { _ = new TimeRange(Start, Start.AddMinutes(-1)); });
    }

    [Fact]
    public void Rejects_instants_that_are_not_in_utc()
    {
        var local = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainException>(() => { _ = new TimeRange(local, local.AddMinutes(30)); });
    }

    [Fact]
    public void Back_to_back_ranges_do_not_overlap()
    {
        var first = new TimeRange(Start, Start.AddMinutes(30));
        var second = new TimeRange(Start.AddMinutes(30), Start.AddMinutes(60));

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
    }

    [Fact]
    public void Detects_overlapping_ranges_from_either_side()
    {
        var first = new TimeRange(Start, Start.AddMinutes(30));
        var second = new TimeRange(Start.AddMinutes(15), Start.AddMinutes(45));

        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
    }
}
