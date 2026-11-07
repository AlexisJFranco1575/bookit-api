using System.Globalization;
using System.Net;
using Bookit.Domain.Notifications;

namespace Bookit.Application.Notifications;

/// <summary>Writes the emails. Pure: the same data always gives the same email, so it is easy to test.</summary>
public static class EmailComposer
{
    public static EmailMessage Compose(NotificationType type, NotificationData data)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(data.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(data.StartsAt, zone);

        var when = local.ToString("dddd, d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);
        var shortWhen = local.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);

        // Owners type these names, so they must never be able to inject line breaks into a header.
        var business = OneLine(data.BusinessName);
        var service = OneLine(data.ServiceName);
        var provider = OneLine(data.ProviderName);

        var (subject, heading, intro, closing) = type switch
        {
            NotificationType.AppointmentBooked => (
                $"Appointment confirmed: {service} on {shortWhen}",
                "Your appointment is confirmed",
                "Thanks for booking. Here are the details of your appointment.",
                $"If your plans change, please contact {business}."),
            NotificationType.AppointmentCancelled => (
                $"Appointment cancelled: {service} on {shortWhen}",
                "Your appointment was cancelled",
                "This appointment has been cancelled.",
                "You can book a new time whenever you like."),
            NotificationType.AppointmentReminder => (
                $"Reminder: {service} on {shortWhen}",
                "Your appointment is coming up",
                "This is a reminder of your upcoming appointment.",
                "See you soon!"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type.")
        };

        var text = string.Join(
            "\n",
            $"Hi {data.CustomerName},",
            string.Empty,
            intro,
            string.Empty,
            $"Business: {business}",
            $"Service: {service}",
            $"With: {provider}",
            $"When: {when} ({data.TimeZoneId})",
            string.Empty,
            closing);

        var html = string.Join(
            "\n",
            "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;color:#1f2933\">",
            $"<h2 style=\"margin:0 0 12px\">{Encode(heading)}</h2>",
            $"<p>Hi {Encode(data.CustomerName)},</p>",
            $"<p>{Encode(intro)}</p>",
            "<table style=\"border-collapse:collapse\">",
            $"<tr><td style=\"padding:4px 16px 4px 0;color:#52606d\">Business</td><td>{Encode(business)}</td></tr>",
            $"<tr><td style=\"padding:4px 16px 4px 0;color:#52606d\">Service</td><td>{Encode(service)}</td></tr>",
            $"<tr><td style=\"padding:4px 16px 4px 0;color:#52606d\">With</td><td>{Encode(provider)}</td></tr>",
            $"<tr><td style=\"padding:4px 16px 4px 0;color:#52606d\">When</td><td>{Encode(when)} ({Encode(data.TimeZoneId)})</td></tr>",
            "</table>",
            $"<p>{Encode(closing)}</p>",
            "</div>");

        return new EmailMessage(data.CustomerEmail, subject, text, html);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string OneLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}