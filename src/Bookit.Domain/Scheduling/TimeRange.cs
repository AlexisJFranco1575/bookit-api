using Bookit.Domain.Common;

namespace Bookit.Domain.Scheduling;

/// <summary>
/// A half-open interval [Start, End) expressed in UTC. Two back-to-back ranges do not overlap,
/// so an appointment ending at 10:00 and another starting at 10:00 can coexist.
/// </summary>
public readonly record struct TimeRange
{
    public TimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
        {
            throw new DomainException("Time ranges must be expressed in UTC.");
        }

        if (end <= start)
        {
            throw new DomainException("A time range must end after it starts.");
        }

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;
}
