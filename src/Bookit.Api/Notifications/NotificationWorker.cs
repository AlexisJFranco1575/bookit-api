using Bookit.Application.Notifications;
using Microsoft.Extensions.Options;

namespace Bookit.Api.Notifications;

/// <summary>
/// Runs inside the API process. Each cycle plans the 24-hour reminders and delivers the pending messages
/// of the outbox. A failing cycle is logged and tried again later; it never stops the API.
///
/// It is written for a single instance of the API. With several instances running at once, two workers could
/// pick up the same pending message, so scaling out would need a lease on each message first.
/// </summary>
public sealed class NotificationWorker(
    IServiceScopeFactory scopes,
    IOptions<NotificationWorkerOptions> options,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private const int MaxBatchesPerCycle = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.WorkerEnabled)
        {
            logger.LogInformation("The notification worker is disabled (Notifications:WorkerEnabled is false).");
            return;
        }

        logger.LogInformation(
            "Notification worker started: a cycle every {Seconds}s, batches of {BatchSize}.",
            settings.IntervalSeconds,
            settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "A notification cycle failed. It will be tried again on the next cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Notification worker stopped.");
    }

    private async Task RunCycleAsync(NotificationWorkerOptions settings, CancellationToken cancellationToken)
    {
        int planned;

        await using (var scope = scopes.CreateAsyncScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
            planned = await notifications.ScheduleRemindersAsync(cancellationToken);
        }

        var sent = 0;
        var failed = 0;
        var skipped = 0;

        // A fresh scope (and database context) per batch, so one bad batch cannot poison the next one.
        for (var batch = 0; batch < MaxBatchesPerCycle; batch++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var result = await notifications.ProcessDueAsync(settings.BatchSize, cancellationToken);

            sent += result.Sent;
            failed += result.Failed;
            skipped += result.Skipped;

            // A short batch means the backlog is drained.
            if (result.Sent + result.Failed + result.Skipped < settings.BatchSize)
            {
                break;
            }
        }

        if (planned + sent + failed + skipped > 0)
        {
            logger.LogInformation(
                "Notification cycle: {Planned} reminders planned, {Sent} sent, {Failed} failed (will retry), {Skipped} skipped.",
                planned,
                sent,
                failed,
                skipped);
        }
    }
}