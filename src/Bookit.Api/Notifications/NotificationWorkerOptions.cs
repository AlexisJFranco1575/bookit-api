namespace Bookit.Api.Notifications;

public sealed class NotificationWorkerOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Turn the worker off to run the API without delivering emails (for example, in some tests).</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Pause between cycles, in seconds.</summary>
    public int IntervalSeconds { get; set; } = 15;

    /// <summary>How many messages are delivered per batch.</summary>
    public int BatchSize { get; set; } = 25;
}