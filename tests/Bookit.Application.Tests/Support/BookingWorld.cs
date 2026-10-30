using Bookit.Application.Booking;
using Bookit.Domain.Businesses;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Tests;

/// <summary>
/// A ready-made scenario: a barbershop in Guayaquil with one haircut (30 min) and one barber who works
/// Mondays 09:00-12:00 and 14:00-17:00. "Now" is Saturday 31 October 2026, so Monday 2 November is bookable.
/// </summary>
internal sealed class BookingWorld
{
    public static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid OwnerId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public FakeBusinessRepository Businesses { get; } = new();

    public FakeServiceRepository Services { get; } = new();

    public FakeProviderRepository Providers { get; } = new();

    public FakeAppointmentRepository Appointments { get; } = new();

    public Business Shop { get; }

    public Service Haircut { get; }

    public Provider Barber { get; }

    public BookingService Booking { get; }

    public BookingWorld()
    {
        Shop = Business.Create(OwnerId, "Barber Shop", "America/Guayaquil", Now);
        Haircut = Service.Create(Shop.Id, "Haircut", 30);
        Barber = Provider.Create(
            Shop.Id,
            "Carlos",
            new WeeklySchedule([
                new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
                new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(17, 0))
            ]));

        Businesses.Items.Add(Shop);
        Services.Items.Add(Haircut);
        Providers.Items.Add(Barber);

        Booking = new BookingService(
            Businesses,
            Services,
            Providers,
            Appointments,
            new SlotCalculator(),
            new FixedTimeProvider(Now));
    }
}