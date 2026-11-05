#pragma warning disable xUnit1051

using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Scheduling;
using Bookit.Domain.Users;
using Bookit.Infrastructure.Persistence;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

public class AgendaDatabaseTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private sealed record Seed(Guid BusinessId, Guid ProviderId, Guid ServiceId);

    /// <summary>A UTC instant on Monday 2 November 2026.</summary>
    private static DateTimeOffset Utc(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddHours(hour).AddMinutes(minute);

    private async Task<Seed> SeedBusinessAsync(string businessName)
    {
        await using var db = fixture.CreateContext();

        var owner = User.Create($"owner-{Guid.NewGuid():N}@example.com", "Owner", "not-a-real-hash", Now);
        var business = Business.Create(owner.Id, businessName, "America/Guayaquil", Now);
        var service = Service.Create(business.Id, "Haircut", 30);
        var provider = Provider.Create(
            business.Id,
            "Carlos",
            new WeeklySchedule([new WorkingPeriod(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]));

        await new UserRepository(db).AddAsync(owner, CancellationToken.None);
        await new BusinessRepository(db).AddAsync(business, CancellationToken.None);
        await new ServiceRepository(db).AddAsync(service, CancellationToken.None);
        await new ProviderRepository(db).AddAsync(provider, CancellationToken.None);

        return new Seed(business.Id, provider.Id, service.Id);
    }

    private async Task BookAsync(Seed seed, DateTimeOffset start)
    {
        await using var db = fixture.CreateContext();

        var appointment = Appointment.Book(
            seed.ProviderId,
            seed.ServiceId,
            "Ana Perez",
            "ana@example.com",
            new TimeRange(start, start.AddMinutes(30)),
            Now);

        Assert.True(await new AppointmentRepository(db).TryAddAsync(appointment, CancellationToken.None));
    }

    [Fact]
    public async Task Lists_only_the_appointments_of_that_business_ordered_by_start_with_names()
    {
        var mine = await SeedBusinessAsync("My Barber Shop");
        var other = await SeedBusinessAsync("Other Barber Shop");

        // Inserted out of order on purpose.
        await BookAsync(mine, Utc(15));
        await BookAsync(mine, Utc(14));
        await BookAsync(other, Utc(14));

        await using var db = fixture.CreateContext();
        var rows = await new AgendaReader(db).ListAsync(
            mine.BusinessId,
            new TimeRange(Utc(0), Utc(24)),
            CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(Utc(14), rows[0].StartsAt);
        Assert.Equal(Utc(15), rows[1].StartsAt);
        Assert.All(rows, row =>
        {
            Assert.Equal("Carlos", row.ProviderName);
            Assert.Equal("Haircut", row.ServiceName);
            Assert.Equal("ana@example.com", row.CustomerEmail);
            Assert.Equal(AppointmentStatus.Booked, row.Status);
        });
    }

    [Fact]
    public async Task The_window_includes_its_start_and_excludes_its_end()
    {
        var seed = await SeedBusinessAsync("Window Barber Shop");

        await BookAsync(seed, Utc(14));
        await BookAsync(seed, Utc(15));
        await BookAsync(seed, Utc(16));

        await using var db = fixture.CreateContext();
        var rows = await new AgendaReader(db).ListAsync(
            seed.BusinessId,
            new TimeRange(Utc(15), Utc(16)),
            CancellationToken.None);

        Assert.Equal(Utc(15), Assert.Single(rows).StartsAt);
    }
}