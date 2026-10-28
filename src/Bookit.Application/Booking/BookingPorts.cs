using Bookit.Domain.Appointments;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Booking;

public interface IAppointmentRepository
{
    /// <summary>The time a provider is already booked for inside a window (booked appointments only).</summary>
    Task<IReadOnlyList<TimeRange>> GetBusyRangesAsync(
        Guid providerId,
        TimeRange window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves the appointment only if it does not overlap another booked appointment of the same provider.
    /// The check and the insert happen atomically, so two simultaneous requests can never both succeed.
    /// Returns false when the time was already taken.
    /// </summary>
    Task<bool> TryAddAsync(Appointment appointment, CancellationToken cancellationToken);

    Task<Appointment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
