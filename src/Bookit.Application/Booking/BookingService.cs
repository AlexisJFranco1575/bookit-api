using Bookit.Application.Catalog;
using Bookit.Application.Common;
using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Booking;

public sealed class BookingService(
    IBusinessRepository businesses,
    IServiceRepository services,
    IProviderRepository providers,
    IAppointmentRepository appointments,
    SlotCalculator calculator,
    TimeProvider clock)
{
    /// <summary>Distance between offered start times.</summary>
    public static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(15);

    /// <summary>A slot must start at least this far from now to be offered.</summary>
    public static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(1);

    public async Task<AvailabilityResponse> GetAvailabilityAsync(
        Guid providerId,
        Guid serviceId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var (business, provider, service) = await LoadAsync(providerId, serviceId, cancellationToken);
        var starts = await ComputeStartsAsync(business, provider, service, from, to, cancellationToken);

        var zone = business.TimeZone;
        var slots = starts
            .Select(start => new SlotResponse(
                start,
                start + service.Duration,
                TimeZoneInfo.ConvertTime(start, zone)))
            .ToList();

        return new AvailabilityResponse(provider.Id, service.Id, business.TimeZoneId, slots);
    }

    public async Task<AppointmentResponse> BookAsync(
        BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var (business, provider, service) = await LoadAsync(request.ProviderId, request.ServiceId, cancellationToken);

        var start = request.StartsAt.ToUniversalTime();
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, business.TimeZone).DateTime);

        // Only times the calculator offers can be booked: inside working hours, on the step grid,
        // far enough from now and free of other appointments.
        var offered = await ComputeStartsAsync(business, provider, service, localDate, localDate, cancellationToken);

        if (!offered.Contains(start))
        {
            throw new ConflictException("That time is not available.");
        }

        var appointment = Appointment.Book(
            provider.Id,
            service.Id,
            request.CustomerName,
            request.CustomerEmail,
            new TimeRange(start, start + service.Duration),
            clock.GetUtcNow());

        // The check above can be stale: someone else may have booked in the meantime. The repository
        // repeats it atomically, and that second check is the one that guarantees no double booking.
        if (!await appointments.TryAddAsync(appointment, cancellationToken))
        {
            throw new ConflictException("That time was just taken by someone else.");
        }

        return AppointmentResponse.From(appointment);
    }

    public async Task<AppointmentResponse> GetAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        AppointmentResponse.From(await RequireAppointmentAsync(appointmentId, cancellationToken));

    public async Task<AppointmentResponse> CancelAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);

        appointment.Cancel(clock.GetUtcNow());
        await appointments.SaveChangesAsync(cancellationToken);

        return AppointmentResponse.From(appointment);
    }

    private async Task<IReadOnlyList<DateTimeOffset>> ComputeStartsAsync(
        Business business,
        Provider provider,
        Service service,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from.Year < 2000 || to.Year > 2100)
        {
            throw new DomainException("The requested dates are out of range.");
        }

        if (to < from)
        {
            throw new DomainException("The end date cannot be before the start date.");
        }

        // Look one day before and two days after, so an appointment that crosses midnight in UTC is never missed.
        var window = new TimeRange(
            new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var busy = await appointments.GetBusyRangesAsync(provider.Id, window, cancellationToken);

        return calculator.GetAvailableStarts(new SlotQuery(
            provider.Schedule,
            business.TimeZone,
            from,
            to,
            service.Duration,
            SlotStep,
            busy,
            clock.GetUtcNow(),
            MinimumNotice));
    }

    private async Task<(Business Business, Provider Provider, Service Service)> LoadAsync(
        Guid providerId,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var provider = await providers.GetAsync(providerId, cancellationToken)
            ?? throw new NotFoundException("Provider", providerId);

        var service = await services.GetAsync(serviceId, cancellationToken)
            ?? throw new NotFoundException("Service", serviceId);

        if (service.BusinessId != provider.BusinessId)
        {
            throw new DomainException("The service and the provider belong to different businesses.");
        }

        var business = await businesses.GetAsync(provider.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Business", provider.BusinessId);

        return (business, provider, service);
    }

    private async Task<Appointment> RequireAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        await appointments.GetAsync(appointmentId, cancellationToken)
        ?? throw new NotFoundException("Appointment", appointmentId);
}
