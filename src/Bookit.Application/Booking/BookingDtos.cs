using Bookit.Domain.Appointments;

namespace Bookit.Application.Booking;

/// <summary>One bookable start time, in UTC and in the business's local time.</summary>
public sealed record SlotResponse(DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset LocalStartsAt);

public sealed record AvailabilityResponse(
    Guid ProviderId,
    Guid ServiceId,
    string TimeZoneId,
    IReadOnlyList<SlotResponse> Slots);

public sealed record BookAppointmentRequest(
    Guid ProviderId,
    Guid ServiceId,
    DateTimeOffset StartsAt,
    string? CustomerName,
    string? CustomerEmail);

public sealed record AppointmentResponse(
    Guid Id,
    Guid ProviderId,
    Guid ServiceId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    AppointmentStatus Status,
    DateTimeOffset CreatedAt)
{
    public static AppointmentResponse From(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.ProviderId,
            appointment.ServiceId,
            appointment.CustomerName,
            appointment.CustomerEmail,
            appointment.StartsAt,
            appointment.EndsAt,
            appointment.Status,
            appointment.CreatedAt);
}
