using System.Net.Mail;
using Bookit.Application.Notifications;
using Bookit.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace Bookit.Api.Notifications;

public static class NotificationExtensions
{
    public static IServiceCollection AddBookitNotifications(this IServiceCollection services)
    {
        // The app refuses to start with an unusable email setup, instead of failing on the first booking.
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .Validate(
                options => options.Mode == EmailMode.Log || !string.IsNullOrWhiteSpace(options.Host),
                "Email:Host is required when Email:Mode is Smtp.")
            .Validate(
                options => options.Port is > 0 and <= 65535,
                "Email:Port must be between 1 and 65535.")
            .Validate(
                options => MailAddress.TryCreate(options.FromAddress, out _),
                "Email:FromAddress must be a valid email address.")
            .Validate(
                options => options.TimeoutSeconds is > 0 and <= 300,
                "Email:TimeoutSeconds must be between 1 and 300.")
            .ValidateOnStart();

        services.AddOptions<NotificationWorkerOptions>()
            .BindConfiguration(NotificationWorkerOptions.SectionName)
            .Validate(
                options => options.IntervalSeconds is >= 1 and <= 3600,
                "Notifications:IntervalSeconds must be between 1 and 3600.")
            .Validate(
                options => options.BatchSize is >= 1 and <= 500,
                "Notifications:BatchSize must be between 1 and 500.")
            .ValidateOnStart();

        // Chosen when first needed, from the settings the host really uses.
        services.AddSingleton<IEmailSender>(provider =>
            provider.GetRequiredService<IOptions<EmailOptions>>().Value.Mode == EmailMode.Smtp
                ? new SmtpEmailSender(provider.GetRequiredService<IOptions<EmailOptions>>())
                : new LoggingEmailSender(provider.GetRequiredService<ILogger<LoggingEmailSender>>()));

        services.AddScoped<NotificationService>();
        services.AddHostedService<NotificationWorker>();

        return services;
    }
}