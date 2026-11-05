using Bookit.Domain.Appointments;

namespace Bookit.Application.Agenda;

public sealed record AgendaItemResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    Guid ServiceId,
    string ServiceName,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset LocalStartsAt,
    AppointmentStatus Status);