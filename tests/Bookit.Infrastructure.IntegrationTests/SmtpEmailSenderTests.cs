#pragma warning disable xUnit1051

using System.Text.Json;
using Bookit.Application.Notifications;
using Bookit.Infrastructure.Email;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

/// <summary>Starts a real Mailpit: a throwaway SMTP server that also exposes the received emails over HTTP.</summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilInternalTcpPortIsAvailable(1025)
            .UntilInternalTcpPortIsAvailable(8025))
        .Build();

    public string Host => _container.Hostname;

    public int SmtpPort => _container.GetMappedPublicPort(1025);

    public string ApiBaseUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8025)}";

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

public class SmtpEmailSenderTests(MailpitFixture mailpit) : IClassFixture<MailpitFixture>
{
    private SmtpEmailSender CreateSender(int? port = null) =>
        new(Options.Create(new EmailOptions
        {
            Mode = EmailMode.Smtp,
            Host = mailpit.Host,
            Port = port ?? mailpit.SmtpPort,
            Security = SecureSocketOptions.None,
            FromAddress = "no-reply@bookit.test",
            FromName = "Bookit Test",
            TimeoutSeconds = 10
        }));

    private async Task<JsonElement?> FindReceivedMessageAsync(string subject)
    {
        using var http = new HttpClient { BaseAddress = new Uri(mailpit.ApiBaseUrl) };

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var json = await http.GetStringAsync("/api/v1/messages");
            using var document = JsonDocument.Parse(json);

            foreach (var message in document.RootElement.GetProperty("messages").EnumerateArray())
            {
                if (message.GetProperty("Subject").GetString() == subject)
                {
                    return message.Clone();
                }
            }

            await Task.Delay(250);
        }

        return null;
    }

    [Fact]
    public async Task A_real_smtp_server_receives_the_email_with_the_right_subject_and_recipient()
    {
        var subject = $"Appointment confirmed {Guid.NewGuid():N}";
        var recipient = $"ana-{Guid.NewGuid():N}@example.com";

        await CreateSender().SendAsync(
            new EmailMessage(recipient, subject, "Plain text body", "<p>Html body</p>"),
            CancellationToken.None);

        var received = await FindReceivedMessageAsync(subject);

        Assert.NotNull(received);
        var to = received.Value.GetProperty("To").EnumerateArray().Single();
        Assert.Equal(recipient, to.GetProperty("Address").GetString());
    }

    [Fact]
    public async Task An_unreachable_smtp_server_makes_the_send_throw_so_the_outbox_can_retry()
    {
        // Nothing listens on port 1, so the connection is refused.
        var sender = CreateSender(port: 1);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sender.SendAsync(
                new EmailMessage("ana@example.com", "Hello", "Body", "<p>Body</p>"),
                CancellationToken.None));
    }
}