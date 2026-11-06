#pragma warning disable xUnit1051

using Bookit.Application.Notifications;
using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Notifications;
using Bookit.Domain.Scheduling;
using Bookit.Domain.Users;
using Bookit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

internal sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public class OutboxDatabaseTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private sealed record Seed(Guid BusinessId, Guid ProviderId, Guid ServiceId);

    /// <summary>A UTC instant on Monday 2 November 2026. Guayaquil is UTC-5, so 14:00 UTC is 09:00 local.</summary>
    private static DateTimeOffset Utc(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddHours(hour).AddMinutes(minute);

    private async Task<Seed> SeedAsync(string businessName = "Outbox Barber Shop")
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

    private static Appointment NewAppointment(Seed seed, DateTimeOffset start, DateTimeOffset createdAt, string email) =>
        Appointment.Book(
            seed.ProviderId,
            seed.ServiceId,
            "Ana Perez",
            email,
            new TimeRange(start, start.AddMinutes(30)),
            createdAt);

    private async Task<Appointment> BookAsync(
        Seed seed,
        DateTimeOffset start,
        DateTimeOffset? createdAt = null,
        string? email = null)
    {
        await using var db = fixture.CreateContext();

        var appointment = NewAppointment(seed, start, createdAt ?? Now, email ?? $"ana-{Guid.NewGuid():N}@example.com");
        Assert.True(await new AppointmentRepository(db).TryAddAsync(appointment, CancellationToken.None));

        return appointment;
    }

    private async Task<List<OutboxMessage>> MessagesForAsync(Guid appointmentId)
    {
        await using var db = fixture.CreateContext();

        return await db.OutboxMessages
            .AsNoTracking()
            .Where(message => message.AppointmentId == appointmentId)
            .ToListAsync();
    }

    private async Task EnqueueRemindersAsync(DateTimeOffset now)
    {
        await using var db = fixture.CreateContext();

        await new OutboxStore(db).EnqueueRemindersAsync(now, TimeSpan.FromHours(24), CancellationToken.None);
    }

    [Fact]
    public async Task Booking_saves_a_pending_confirmation_in_the_same_transaction()
    {
        var seed = await SeedAsync();

        var appointment = await BookAsync(seed, Utc(14));

        var message = Assert.Single(await MessagesForAsync(appointment.Id));
        Assert.Equal(NotificationType.AppointmentBooked, message.Type);
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(Now, message.NextAttemptAt);
    }

    [Fact]
    public async Task A_booking_that_loses_the_race_leaves_no_notice_behind()
    {
        var seed = await SeedAsync();
        await BookAsync(seed, Utc(14));

        await using (var db = fixture.CreateContext())
        {
            var loser = NewAppointment(seed, Utc(14), Now, $"late-{Guid.NewGuid():N}@example.com");

            Assert.False(await new AppointmentRepository(db).TryAddAsync(loser, CancellationToken.None));
        }

        await using var check = fixture.CreateContext();
        var notices = await check.OutboxMessages.CountAsync(message =>
            check.Appointments.Any(appointment =>
                appointment.Id == message.AppointmentId && appointment.ProviderId == seed.ProviderId));

        Assert.Equal(1, notices);
    }

    [Fact]
    public async Task Cancelling_saves_exactly_one_cancellation_notice()
    {
        var seed = await SeedAsync();
        var appointment = await BookAsync(seed, Utc(14));

        await using (var db = fixture.CreateContext())
        {
            var repository = new AppointmentRepository(db);
            var loaded = await repository.GetAsync(appointment.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Cancel(Now.AddHours(1));

            await repository.SaveChangesAsync(CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None); // saving again must not queue a second notice
        }

        var messages = await MessagesForAsync(appointment.Id);

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, message => message.Type == NotificationType.AppointmentBooked);
        Assert.Contains(messages, message => message.Type == NotificationType.AppointmentCancelled);
    }

    [Fact]
    public async Task A_reminder_is_saved_once_when_the_appointment_is_inside_the_24_hour_window()
    {
        var seed = await SeedAsync();
        var appointment = await BookAsync(seed, Utc(14)); // booked two days ahead
        var now = Utc(14).AddHours(-20);

        await EnqueueRemindersAsync(now);
        await EnqueueRemindersAsync(now); // a second planner run must not duplicate it

        var reminder = Assert.Single(
            await MessagesForAsync(appointment.Id),
            message => message.Type == NotificationType.AppointmentReminder);

        Assert.Equal(OutboxStatus.Pending, reminder.Status);
        Assert.Equal(now, reminder.NextAttemptAt);
    }

    [Fact]
    public async Task No_reminder_is_saved_before_the_24_hour_window()
    {
        var seed = await SeedAsync();
        var appointment = await BookAsync(seed, Utc(14));

        await EnqueueRemindersAsync(Utc(14).AddHours(-30));

        Assert.DoesNotContain(
            await MessagesForAsync(appointment.Id),
            message => message.Type == NotificationType.AppointmentReminder);
    }

    [Fact]
    public async Task A_short_notice_booking_gets_a_skipped_reminder_instead_of_a_real_one()
    {
        var seed = await SeedAsync();
        var appointment = await BookAsync(seed, Utc(14), createdAt: Utc(14).AddHours(-3)); // booked 3 hours ahead

        await EnqueueRemindersAsync(Utc(14).AddHours(-2));

        var reminder = Assert.Single(
            await MessagesForAsync(appointment.Id),
            message => message.Type == NotificationType.AppointmentReminder);

        Assert.Equal(OutboxStatus.Skipped, reminder.Status);
    }

    [Fact]
    public async Task Five_planners_running_at_once_create_a_single_reminder_without_errors()
    {
        var seed = await SeedAsync();
        var appointment = await BookAsync(seed, Utc(14));
        var now = Utc(14).AddHours(-20);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => EnqueueRemindersAsync(now)));

        Assert.Single(
            await MessagesForAsync(appointment.Id),
            message => message.Type == NotificationType.AppointmentReminder);
    }

    [Fact]
    public async Task Claiming_returns_only_pending_messages_whose_time_has_come()
    {
        var seed = await SeedAsync();
        var first = await BookAsync(seed, Utc(14));
        var second = await BookAsync(seed, Utc(15));

        await using (var db = fixture.CreateContext())
        {
            var store = new OutboxStore(db);
            var due = await store.ClaimDueAsync(Now, 1000, CancellationToken.None);

            Assert.Contains(due, message => message.AppointmentId == first.Id);
            Assert.Contains(due, message => message.AppointmentId == second.Id);

            due.Single(message => message.AppointmentId == first.Id).MarkSent(Now);
            await store.SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = fixture.CreateContext())
        {
            var due = await new OutboxStore(db).ClaimDueAsync(Now, 1000, CancellationToken.None);

            Assert.DoesNotContain(due, message => message.AppointmentId == first.Id);
            Assert.Contains(due, message => message.AppointmentId == second.Id);
        }

        await using (var db = fixture.CreateContext())
        {
            var early = await new OutboxStore(db).ClaimDueAsync(Now.AddMinutes(-1), 1000, CancellationToken.None);

            Assert.DoesNotContain(early, message => message.AppointmentId == second.Id);
        }
    }

    [Fact]
    public async Task The_data_reader_returns_the_names_and_the_time_zone_for_the_email()
    {
        var seed = await SeedAsync("Reader Barber Shop");
        var email = $"ana-{Guid.NewGuid():N}@example.com";
        var appointment = await BookAsync(seed, Utc(14), email: email);

        await using var db = fixture.CreateContext();
        var reader = new NotificationDataReader(db);

        var data = await reader.GetAsync(appointment.Id, CancellationToken.None);

        Assert.NotNull(data);
        Assert.Equal("Reader Barber Shop", data.BusinessName);
        Assert.Equal("Haircut", data.ServiceName);
        Assert.Equal("Carlos", data.ProviderName);
        Assert.Equal("America/Guayaquil", data.TimeZoneId);
        Assert.Equal(email, data.CustomerEmail);
        Assert.Equal(AppointmentStatus.Booked, data.Status);
        Assert.Equal(Utc(14), data.StartsAt);

        Assert.Null(await reader.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task A_booking_and_its_cancellation_are_delivered_as_emails_exactly_once()
    {
        var seed = await SeedAsync("Delivery Barber Shop");
        var email = $"ana-{Guid.NewGuid():N}@example.com";
        var appointment = await BookAsync(seed, Utc(14), email: email);
        var sender = new RecordingEmailSender();

        // Round 1: the confirmation goes out.
        await using (var db = fixture.CreateContext())
        {
            var service = new NotificationService(
                new OutboxStore(db),
                new NotificationDataReader(db),
                sender,
                new FixedTimeProvider(Now));

            await service.ProcessDueAsync(1000, CancellationToken.None);
            await service.ProcessDueAsync(1000, CancellationToken.None); // nothing more to send
        }

        var confirmation = Assert.Single(sender.Sent, message => message.To == email);
        Assert.Equal("Appointment confirmed: Haircut on Mon 2 Nov, 09:00", confirmation.Subject);
        Assert.Contains("Delivery Barber Shop", confirmation.TextBody);

        // The owner or customer cancels.
        await using (var db = fixture.CreateContext())
        {
            var repository = new AppointmentRepository(db);
            var loaded = await repository.GetAsync(appointment.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Cancel(Now.AddHours(1));
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        // Round 2, a little later: the cancellation notice goes out.
        await using (var db = fixture.CreateContext())
        {
            var service = new NotificationService(
                new OutboxStore(db),
                new NotificationDataReader(db),
                sender,
                new FixedTimeProvider(Now.AddHours(2)));

            await service.ProcessDueAsync(1000, CancellationToken.None);
        }

        var delivered = sender.Sent.Where(message => message.To == email).ToList();

        Assert.Equal(2, delivered.Count);
        Assert.StartsWith("Appointment cancelled:", delivered[1].Subject);

        var messages = await MessagesForAsync(appointment.Id);
        Assert.All(messages, message => Assert.Equal(OutboxStatus.Sent, message.Status));
    }
}