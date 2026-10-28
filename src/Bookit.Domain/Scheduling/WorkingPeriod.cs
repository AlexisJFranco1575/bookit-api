namespace Bookit.Domain.Scheduling;

/// <summary>
/// A block of working time on one weekday, in the business's local time (for example Monday 09:00 to 12:00).
/// A period cannot cross midnight; split it into two days instead.
/// </summary>
public sealed record WorkingPeriod(DayOfWeek Day, TimeOnly Start, TimeOnly End);
