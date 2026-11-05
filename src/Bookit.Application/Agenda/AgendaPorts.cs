using Bookit.Domain.Appointments;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Agenda;

/// <summary>An appointment together with the names the owner needs to read the agenda at a glance.</summary>
public sealed record AgendaRow(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    Guid ServiceId,
    string ServiceName,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    AppointmentStatus Status);

/// <summary>Read-only query for the owner's agenda. Kept apart from the booking repository on purpose.</summary>
public interface IAgendaReader
{
    /// <summary>
    /// Appointments of a business that start inside the window (start included, end excluded),
    /// in any status, ordered by start time.
    /// </summary>
    Task<IReadOnlyList<AgendaRow>> ListAsync(Guid businessId, TimeRange window, CancellationToken cancellationToken);
}