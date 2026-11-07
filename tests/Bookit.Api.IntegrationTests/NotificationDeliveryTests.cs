#pragma warning disable xUnit1051

using System.Collections.Concurrent;
using System.Net;
using Bookit.Application.Booking;
using Bookit.Application.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bookit.Api.IntegrationTests;

internal sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

[Collection(ApiCollection.Name)]
public class NotificationDeliveryTests(ApiFactory factory)
{
    private static async Task<EmailMessage> WaitForEmailAsync(RecordingEmailSender sender, string to, string subjectStart)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var email = sender.Sent.FirstOrDefault(message =>
                message.To == to && message.Subject.StartsWith(subjectStart, StringComparison.Ordinal));

            if (email is not null)
            {
                return email;
            }

            await Task.Delay(250);
        }

        Assert.Fail($"No email starting with '{subjectStart}' reached {to} within 30 seconds.");
        return null!;
    }

    [Fact]
    public async Task A_booking_and_a_cancellation_made_over_http_are_emailed_by_the_background_worker()
    {
        var sender = new RecordingEmailSender();

        // A second copy of the API, on the same database, with the worker switched on and a recording sender.
        using var withWorker = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Notifications:WorkerEnabled"] = "true",
                    ["Notifications:IntervalSeconds"] = "1"
                }));

            builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(sender));
        });

        using var workerClient = withWorker.CreateClient(); // creating a client starts the host, and the worker with it

        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();
        var customer = factory.CreateClient();
        var customerEmail = $"ana-{Guid.NewGuid():N}@example.com";

        var availability = await customer.GetAvailabilityAsync(shop, TestHelpers.NextMonday());
        var booked = await customer.BookAsync(shop, availability.Slots[0], customerEmail);
        await booked.ShouldBeAsync(HttpStatusCode.Created);
        var appointment = await booked.ReadAsync<AppointmentResponse>();

        var confirmation = await WaitForEmailAsync(sender, customerEmail, "Appointment confirmed");
        Assert.Contains("Haircut", confirmation.Subject);
        Assert.Contains("09:00", confirmation.Subject);

        var cancelled = await customer.PostAsync($"/api/appointments/{appointment.Id}/cancel", null);
        await cancelled.ShouldBeAsync(HttpStatusCode.OK);

        var cancellation = await WaitForEmailAsync(sender, customerEmail, "Appointment cancelled");
        Assert.Contains("has been cancelled", cancellation.TextBody);

        // Each notice was delivered exactly once.
        Assert.Single(sender.Sent, message => message.To == customerEmail && message.Subject.StartsWith("Appointment confirmed"));
        Assert.Single(sender.Sent, message => message.To == customerEmail && message.Subject.StartsWith("Appointment cancelled"));
    }
}