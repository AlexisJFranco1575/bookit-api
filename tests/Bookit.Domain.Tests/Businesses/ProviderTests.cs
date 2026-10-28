using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;
using Xunit;

namespace Bookit.Domain.Tests.Businesses;

public class ProviderTests
{
    private static WeeklySchedule Mondays(int startHour, int endHour) =>
        new([new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0))]);

    [Fact]
    public void Creates_a_provider_with_its_schedule()
    {
        var provider = Provider.Create(Guid.NewGuid(), "Carlos", Mondays(9, 12));

        Assert.Equal("Carlos", provider.Name);
        Assert.Single(provider.WorkingPeriods);
        Assert.Single(provider.Schedule.Periods);
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => { _ = Provider.Create(Guid.NewGuid(), " ", Mondays(9, 12)); });
    }

    [Fact]
    public void Replaces_the_whole_schedule()
    {
        var provider = Provider.Create(Guid.NewGuid(), "Carlos", Mondays(9, 12));

        provider.ReplaceSchedule(Mondays(14, 17));

        Assert.Single(provider.WorkingPeriods);
        Assert.Equal(new TimeOnly(14, 0), provider.WorkingPeriods[0].Start);
    }
}
