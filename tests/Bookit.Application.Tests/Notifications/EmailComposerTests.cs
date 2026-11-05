using Bookit.Application.Notifications;
using Bookit.Domain.Appointments;
using Bookit.Domain.Notifications;
using Xunit;

namespace Bookit.Application.Tests;

public class EmailComposerTests
{
    // 14:00 UTC on Monday 2 November 2026 is 09:00 in Guayaquil (UTC-5, no daylight saving).
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 14, 0, 0, TimeSpan.Zero);

    private static NotificationData Data(
        string customerName = "Ana Perez",
        string serviceName = "Haircut",
        string timeZoneId = "America/Guayaquil",
        DateTimeOffset? startsAt = null) =>
        new(
            Guid.NewGuid(),
            AppointmentStatus.Booked,
            customerName,
            "ana@example.com",
            startsAt ?? Start,
            (startsAt ?? Start).AddMinutes(30),
            serviceName,
            "Carlos",
            "Barber Shop",
            timeZoneId);

    [Fact]
    public void The_confirmation_goes_to_the_customer_with_the_local_time_and_the_details()
    {
        var email = EmailComposer.Compose(NotificationType.AppointmentBooked, Data());

        Assert.Equal("ana@example.com", email.To);
        Assert.Equal("Appointment confirmed: Haircut on Mon 2 Nov, 09:00", email.Subject);
        Assert.Contains("Hi Ana Perez,", email.TextBody);
        Assert.Contains("Monday, 2 November 2026 at 09:00 (America/Guayaquil)", email.TextBody);
        Assert.Contains("Service: Haircut", email.TextBody);
        Assert.Contains("With: Carlos", email.TextBody);
        Assert.Contains("Business: Barber Shop", email.TextBody);
        Assert.Contains("09:00", email.HtmlBody);
    }

    [Fact]
    public void The_cancellation_notice_says_so()
    {
        var email = EmailComposer.Compose(NotificationType.AppointmentCancelled, Data());

        Assert.StartsWith("Appointment cancelled:", email.Subject);
        Assert.Contains("has been cancelled", email.TextBody);
    }

    [Fact]
    public void The_reminder_says_so()
    {
        var email = EmailComposer.Compose(NotificationType.AppointmentReminder, Data());

        Assert.StartsWith("Reminder:", email.Subject);
        Assert.Contains("reminder of your upcoming appointment", email.TextBody);
    }

    [Fact]
    public void The_html_version_escapes_what_users_typed()
    {
        var email = EmailComposer.Compose(
            NotificationType.AppointmentBooked,
            Data(customerName: "Ana <b>Perez</b> & Co"));

        Assert.Contains("Ana &lt;b&gt;Perez&lt;/b&gt; &amp; Co", email.HtmlBody);
        Assert.DoesNotContain("<b>Perez</b>", email.HtmlBody);
    }

    [Fact]
    public void The_subject_never_contains_line_breaks()
    {
        var email = EmailComposer.Compose(
            NotificationType.AppointmentBooked,
            Data(serviceName: "Hair\r\nBcc: evil@example.com"));

        Assert.DoesNotContain('\r', email.Subject);
        Assert.DoesNotContain('\n', email.Subject);
    }

    [Fact]
    public void The_time_is_shown_in_the_business_time_zone_including_daylight_saving()
    {
        // New York changed to daylight time on 8 March 2026, so 13:00 UTC on the 9th is 09:00 local (UTC-4).
        var email = EmailComposer.Compose(
            NotificationType.AppointmentBooked,
            Data(timeZoneId: "America/New_York", startsAt: new DateTimeOffset(2026, 3, 9, 13, 0, 0, TimeSpan.Zero)));

        Assert.Contains("Monday, 9 March 2026 at 09:00 (America/New_York)", email.TextBody);
    }
}