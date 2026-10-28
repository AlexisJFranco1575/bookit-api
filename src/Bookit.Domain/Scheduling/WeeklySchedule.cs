using Bookit.Domain.Common;

namespace Bookit.Domain.Scheduling;

/// <summary>The recurring weekly working hours of a provider. A day may have several periods (a lunch break).</summary>
public sealed class WeeklySchedule
{
    private readonly List<WorkingPeriod> _periods;

    public WeeklySchedule(IEnumerable<WorkingPeriod> periods)
    {
        var list = periods.ToList();

        foreach (var period in list)
        {
            if (period.End <= period.Start)
            {
                throw new DomainException($"A working period on {period.Day} must end after it starts.");
            }
        }

        foreach (var day in list.GroupBy(period => period.Day))
        {
            var ordered = day.OrderBy(period => period.Start).ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    throw new DomainException($"Working periods on {day.Key} overlap.");
                }
            }
        }

        _periods = list.OrderBy(period => period.Day).ThenBy(period => period.Start).ToList();
    }

    public IReadOnlyList<WorkingPeriod> Periods => _periods;

    public IEnumerable<WorkingPeriod> For(DayOfWeek day) => _periods.Where(period => period.Day == day);
}
