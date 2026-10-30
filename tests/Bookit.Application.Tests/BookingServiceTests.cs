#pragma warning disable xUnit1051

using Bookit.Application.Booking;
using Bookit.Application.Common;
using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Xunit;

namespace Bookit.Application.Tests;

public class BookingServiceTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private readonly BookingWorld _world = new();

    // Guayaquil is UTC-5 all year, so 09:00 local is 14:00 UTC. Hours are added (not passed to the
    // constructor) so a local time late in the evening still becomes a valid instant.
    private static DateTimeOffset MondayAt(int localHour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.Zero).AddHours(localHour).AddMinutes(minute);

    private Task<AvailabilityResponse> AvailabilityAsync() =>
        _world.Booking.GetAvailabilityAsync(
            _world.Barber.Id, _world.Haircut.Id, Monday, Monday, CancellationToken.None);

    private Task<AppointmentResponse> BookAsync(DateTimeOffset start, string email = "ana@example.com") =>
        _world.Booking.BookAsync(
            new BookAppointmentRequest(_world.Barber.Id, _world.Haircut.Id, start, "Ana Perez", email),
            CancellationToken.None);

    [Fact]
    public async Task Lists_the_slots_of_a_working_day_in_utc_and_local_time()
    {
        var availability = await AvailabilityAsync();

        Assert.Equal(22, availability.Slots.Count);
        Assert.Equal(MondayAt(9), availability.Slots[0].StartsAt);
        Assert.Equal(MondayAt(9, 30), availability.Slots[0].EndsAt);
        Assert.Equal(TimeSpan.FromHours(-5), availability.Slots[0].LocalStartsAt.Offset);
        Assert.Equal(9, availability.Slots[0].LocalStartsAt.Hour);
        Assert.Equal("America/Guayaquil", availability.TimeZoneId);
    }

    [Fact]
    public async Task Books_an_offered_slot_and_removes_the_slots_it_overlaps()
    {
        var appointment = await BookAsync(MondayAt(9));

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Equal(MondayAt(9), appointment.StartsAt);
        Assert.Equal(MondayAt(9, 30), appointment.EndsAt);

        var availability = await AvailabilityAsync();

        Assert.Equal(20, availability.Slots.Count);
        Assert.DoesNotContain(availability.Slots, slot => slot.StartsAt == MondayAt(9));
        Assert.DoesNotContain(availability.Slots, slot => slot.StartsAt == MondayAt(9, 15));
        Assert.Contains(availability.Slots, slot => slot.StartsAt == MondayAt(9, 30));
    }

    [Fact]
    public async Task Rejects_a_time_outside_the_working_hours()
    {
        await Assert.ThrowsAsync<ConflictException>(() => BookAsync(MondayAt(20)));
    }

    [Fact]
    public async Task Rejects_a_time_that_is_not_on_the_slot_grid()
    {
        await Assert.ThrowsAsync<ConflictException>(() => BookAsync(MondayAt(9, 10)));
    }

    [Fact]
    public async Task Rejects_a_slot_that_is_already_taken()
    {
        await BookAsync(MondayAt(9));

        await Assert.ThrowsAsync<ConflictException>(() => BookAsync(MondayAt(9)));
        await Assert.ThrowsAsync<ConflictException>(() => BookAsync(MondayAt(9, 15)));
    }

    [Fact]
    public async Task Reports_a_conflict_when_the_slot_is_taken_between_the_check_and_the_insert()
    {
        _world.Appointments.ForceConflict = true;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => BookAsync(MondayAt(9)));

        Assert.Contains("just taken", exception.Message);
        Assert.Empty(_world.Appointments.Items);
    }

    [Fact]
    public async Task Fails_with_not_found_for_an_unknown_provider()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _world.Booking.GetAvailabilityAsync(
                Guid.NewGuid(), _world.Haircut.Id, Monday, Monday, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_a_service_from_another_business()
    {
        var other = Business.Create(BookingWorld.OwnerId, "Other Shop", "America/Guayaquil", BookingWorld.Now);
        var foreignService = Service.Create(other.Id, "Massage", 60);
        _world.Businesses.Items.Add(other);
        _world.Services.Items.Add(foreignService);

        await Assert.ThrowsAsync<DomainException>(() =>
            _world.Booking.BookAsync(
                new BookAppointmentRequest(
                    _world.Barber.Id, foreignService.Id, MondayAt(9), "Ana Perez", "ana@example.com"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Cancelling_an_appointment_frees_its_slot()
    {
        var appointment = await BookAsync(MondayAt(9));

        var cancelled = await _world.Booking.CancelAsync(appointment.Id, CancellationToken.None);

        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);

        var availability = await AvailabilityAsync();

        Assert.Equal(22, availability.Slots.Count);
        Assert.Contains(availability.Slots, slot => slot.StartsAt == MondayAt(9));
    }

    [Fact]
    public async Task Cancelling_an_unknown_appointment_fails_with_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _world.Booking.CancelAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_an_invalid_email_and_stores_nothing()
    {
        await Assert.ThrowsAsync<DomainException>(() => BookAsync(MondayAt(9), "not-an-email"));

        Assert.Empty(_world.Appointments.Items);
    }
}