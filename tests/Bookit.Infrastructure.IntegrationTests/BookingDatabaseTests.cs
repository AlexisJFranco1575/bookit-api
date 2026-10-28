#pragma warning disable xUnit1051

using Bookit.Application.Booking;
using Bookit.Application.Common;
using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Scheduling;
using Bookit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

public class BookingDatabaseTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static readonly WeeklySchedule Schedule = new([
        new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
        new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(17, 0))
    ]);

    private sealed record Seed(Guid BusinessId, Guid ProviderId, Guid ServiceId);

    /// <summary>A UTC instant on Monday 2 November 2026. Guayaquil is UTC-5, so 14:00 UTC is 09:00 local.</summary>
    private static DateTimeOffset Utc(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddHours(hour).AddMinutes(minute);

    private async Task<Seed> SeedAsync()
    {
        await using var db = fixture.CreateContext();

        var business = Business.Create("Barber Shop", "America/Guayaquil", Now);
        var service = Service.Create(business.Id, "Haircut", 30);
        var provider = Provider.Create(business.Id, "Carlos", Schedule);

        await new BusinessRepository(db).AddAsync(business, CancellationToken.None);
        await new ServiceRepository(db).AddAsync(service, CancellationToken.None);
        await new ProviderRepository(db).AddAsync(provider, CancellationToken.None);

        return new Seed(business.Id, provider.Id, service.Id);
    }

    private async Task<Guid> AddProviderAsync(Guid businessId, string name)
    {
        await using var db = fixture.CreateContext();

        var provider = Provider.Create(businessId, name, Schedule);
        await new ProviderRepository(db).AddAsync(provider, CancellationToken.None);

        return provider.Id;
    }

    private static Appointment NewAppointment(
        Guid providerId,
        Guid serviceId,
        DateTimeOffset start,
        int minutes,
        string label = "customer") =>
        Appointment.Book(
            providerId,
            serviceId,
            "Test Customer",
            $"{label}@example.com",
            new TimeRange(start, start.AddMinutes(minutes)),
            Now);

    /// <summary>Each attempt uses its own context, exactly like separate web requests would.</summary>
    private async Task<bool> TryBookAsync(Appointment appointment)
    {
        await using var db = fixture.CreateContext();

        return await new AppointmentRepository(db).TryAddAsync(appointment, CancellationToken.None);
    }

    [Fact]
    public async Task Twenty_simultaneous_requests_for_the_same_slot_create_exactly_one_appointment()
    {
        var seed = await SeedAsync();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(i =>
                TryBookAsync(NewAppointment(seed.ProviderId, seed.ServiceId, Utc(14), 30, $"c{i}"))));

        Assert.Equal(1, results.Count(won => won));

        await using var check = fixture.CreateContext();
        Assert.Equal(1, await check.Appointments.CountAsync(a => a.ProviderId == seed.ProviderId));
    }

    [Fact]
    public async Task Simultaneous_requests_with_different_start_times_cannot_overlap()
    {
        var seed = await SeedAsync();

        // Half start at 09:00 and half at 09:30, each for an hour: every pair overlaps, so only one can win.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(i =>
                TryBookAsync(NewAppointment(
                    seed.ProviderId,
                    seed.ServiceId,
                    i % 2 == 0 ? Utc(14) : Utc(14, 30),
                    60,
                    $"c{i}"))));

        Assert.Equal(1, results.Count(won => won));
    }

    [Fact]
    public async Task Back_to_back_appointments_are_both_accepted()
    {
        var seed = await SeedAsync();

        var first = await TryBookAsync(NewAppointment(seed.ProviderId, seed.ServiceId, Utc(14), 30, "first"));
        var second = await TryBookAsync(NewAppointment(seed.ProviderId, seed.ServiceId, Utc(14, 30), 30, "second"));

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public async Task Different_providers_do_not_block_each_other()
    {
        var seed = await SeedAsync();
        var otherProviderId = await AddProviderAsync(seed.BusinessId, "Marta");

        var results = await Task.WhenAll(
            TryBookAsync(NewAppointment(seed.ProviderId, seed.ServiceId, Utc(14), 30, "carlos")),
            TryBookAsync(NewAppointment(otherProviderId, seed.ServiceId, Utc(14), 30, "marta")));

        Assert.All(results, Assert.True);
    }

    [Fact]
    public async Task A_cancelled_appointment_frees_its_slot()
    {
        var seed = await SeedAsync();
        var first = NewAppointment(seed.ProviderId, seed.ServiceId, Utc(14), 30, "first");
        Assert.True(await TryBookAsync(first));

        await using (var db = fixture.CreateContext())
        {
            var repository = new AppointmentRepository(db);
            var loaded = await repository.GetAsync(first.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Cancel(Now.AddHours(1));
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        var second = NewAppointment(seed.ProviderId, seed.ServiceId, Utc(14), 30, "second");

        Assert.True(await TryBookAsync(second));
    }

    [Fact]
    public async Task The_provider_schedule_survives_a_round_trip_through_the_database()
    {
        var seed = await SeedAsync();

        await using var db = fixture.CreateContext();
        var provider = await new ProviderRepository(db).GetAsync(seed.ProviderId, CancellationToken.None);

        Assert.NotNull(provider);
        Assert.Equal(2, provider.WorkingPeriods.Count);
        Assert.Contains(
            new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            provider.WorkingPeriods);
        Assert.Contains(
            new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(17, 0)),
            provider.WorkingPeriods);
    }

    [Fact]
    public async Task The_database_itself_rejects_two_booked_appointments_with_the_same_start()
    {
        var seed = await SeedAsync();

        await using var db = fixture.CreateContext();
        db.Appointments.Add(NewAppointment(seed.ProviderId, seed.ServiceId, Utc(15), 30, "first"));
        await db.SaveChangesAsync();

        // Skipping the repository (and its lock) on purpose: the unique index is the last line of defence.
        db.Appointments.Add(NewAppointment(seed.ProviderId, seed.ServiceId, Utc(15), 30, "second"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_booking_service_books_end_to_end_against_a_real_database()
    {
        var seed = await SeedAsync();

        await using var db = fixture.CreateContext();
        var booking = new BookingService(
            new BusinessRepository(db),
            new ServiceRepository(db),
            new ProviderRepository(db),
            new AppointmentRepository(db),
            new SlotCalculator(),
            new FixedTimeProvider(Now));

        var monday = new DateOnly(2026, 11, 2);

        var before = await booking.GetAvailabilityAsync(
            seed.ProviderId, seed.ServiceId, monday, monday, CancellationToken.None);
        Assert.Equal(22, before.Slots.Count);

        var firstSlot = before.Slots[0].StartsAt;
        var request = new BookAppointmentRequest(
            seed.ProviderId, seed.ServiceId, firstSlot, "Ana Perez", "ana@example.com");

        var appointment = await booking.BookAsync(request, CancellationToken.None);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);

        var after = await booking.GetAvailabilityAsync(
            seed.ProviderId, seed.ServiceId, monday, monday, CancellationToken.None);
        Assert.Equal(20, after.Slots.Count);

        await Assert.ThrowsAsync<ConflictException>(() => booking.BookAsync(request, CancellationToken.None));
    }
}
