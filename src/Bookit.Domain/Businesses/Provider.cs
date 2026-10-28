using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;

namespace Bookit.Domain.Businesses;

/// <summary>A person who performs services and takes appointments, with their own weekly working hours.</summary>
public sealed class Provider
{
    public const int MaxNameLength = 100;

    private readonly List<WorkingPeriod> _workingPeriods = [];

    private Provider()
    {
    }

    public Guid Id { get; private set; }

    public Guid BusinessId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyList<WorkingPeriod> WorkingPeriods => _workingPeriods;

    public WeeklySchedule Schedule => new(_workingPeriods);

    public static Provider Create(Guid businessId, string? name, WeeklySchedule schedule)
    {
        if (businessId == Guid.Empty)
        {
            throw new DomainException("A business is required.");
        }

        var cleanName = name?.Trim() ?? string.Empty;

        if (cleanName.Length is 0 or > MaxNameLength)
        {
            throw new DomainException($"The provider name is required and can have at most {MaxNameLength} characters.");
        }

        var provider = new Provider
        {
            Id = Guid.CreateVersion7(),
            BusinessId = businessId,
            Name = cleanName
        };

        provider._workingPeriods.AddRange(schedule.Periods);

        return provider;
    }

    public void ReplaceSchedule(WeeklySchedule schedule)
    {
        _workingPeriods.Clear();
        _workingPeriods.AddRange(schedule.Periods);
    }
}
