#pragma warning disable xUnit1051

using Bookit.Application.Agenda;
using Bookit.Application.Common;
using Bookit.Domain.Appointments;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;
using Xunit;

namespace Bookit.Application.Tests;

public class AgendaServiceTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private readonly BookingWorld _world = new();
    private readonly FakeAgendaReader _reader = new();
    private readonly AgendaService _agenda;

    public AgendaServiceTests()
    {
        _agenda = new AgendaService(_world.Businesses, _reader);
    }

    /// <summary>A UTC instant in November 2026. Guayaquil is UTC-5, so 14:00 UTC is 09:00 local.</summary>
    private static DateTimeOffset Utc(int day, int hour) =>
        new DateTimeOffset(2026, 11, day, 0, 0, 0, TimeSpan.Zero).AddHours(hour);

    private AgendaRow Row(DateTimeOffset start) =>
        new(
            Guid.NewGuid(),
            _world.Barber.Id,
            "Carlos",
            _world.Haircut.Id,
            "Haircut",
            "Ana Perez",
            "ana@example.com",
            start,
            start.AddMinutes(30),
            AppointmentStatus.Booked);

    [Fact]
    public async Task Returns_the_appointments_of_the_local_day_ordered_with_names_and_local_time()
    {
        _reader.Rows.Add(Row(Utc(3, 4)));  // Monday 23:00 local: still Monday
        _reader.Rows.Add(Row(Utc(2, 14))); // Monday 09:00 local
        _reader.Rows.Add(Row(Utc(2, 4)));  // Sunday 23:00 local: before Monday
        _reader.Rows.Add(Row(Utc(3, 5)));  // Tuesday 00:00 local: after Monday

        var items = await _agenda.GetAgendaAsync(
            BookingWorld.OwnerId, _world.Shop.Id, Monday, Monday, CancellationToken.None);

        Assert.Equal(2, items.Count);

        Assert.Equal(9, items[0].LocalStartsAt.Hour);
        Assert.Equal("Carlos", items[0].ProviderName);
        Assert.Equal("Haircut", items[0].ServiceName);
        Assert.Equal("ana@example.com", items[0].CustomerEmail);

        Assert.Equal(23, items[1].LocalStartsAt.Hour);
        Assert.Equal(2, items[1].LocalStartsAt.Day);
        Assert.Equal(TimeSpan.FromHours(-5), items[1].LocalStartsAt.Offset);
    }

    [Fact]
    public async Task Turns_local_days_into_the_matching_utc_window()
    {
        await _agenda.GetAgendaAsync(
            BookingWorld.OwnerId, _world.Shop.Id, Monday, Monday.AddDays(1), CancellationToken.None);

        // Monday 00:00 local (05:00 UTC) up to Wednesday 00:00 local (05:00 UTC).
        Assert.Equal(new TimeRange(Utc(2, 5), Utc(4, 5)), _reader.LastWindow);
    }

    [Fact]
    public async Task Another_owner_gets_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _agenda.GetAgendaAsync(Guid.NewGuid(), _world.Shop.Id, Monday, Monday, CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_business_gets_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _agenda.GetAgendaAsync(BookingWorld.OwnerId, Guid.NewGuid(), Monday, Monday, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_an_end_date_before_the_start_date()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _agenda.GetAgendaAsync(
                BookingWorld.OwnerId, _world.Shop.Id, Monday, Monday.AddDays(-1), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_a_range_longer_than_62_days()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _agenda.GetAgendaAsync(
                BookingWorld.OwnerId, _world.Shop.Id, Monday, Monday.AddDays(62), CancellationToken.None));

        // 62 days exactly is allowed.
        var items = await _agenda.GetAgendaAsync(
            BookingWorld.OwnerId, _world.Shop.Id, Monday, Monday.AddDays(61), CancellationToken.None);

        Assert.Empty(items);
    }
}