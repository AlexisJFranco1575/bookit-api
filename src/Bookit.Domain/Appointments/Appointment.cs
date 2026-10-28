using System.Net.Mail;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;

namespace Bookit.Domain.Appointments;

/// <summary>One customer's booking of one provider for one service. All instants are UTC.</summary>
public sealed class Appointment
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 254;

    // Persistence will need a parameterless constructor later; the factory below is the only public way in.
    private Appointment()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProviderId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public TimeRange Range => new(StartsAt, EndsAt);

    public static Appointment Book(
        Guid providerId,
        Guid serviceId,
        string? customerName,
        string? customerEmail,
        TimeRange range,
        DateTimeOffset now)
    {
        if (providerId == Guid.Empty)
        {
            throw new DomainException("A provider is required.");
        }

        if (serviceId == Guid.Empty)
        {
            throw new DomainException("A service is required.");
        }

        if (range.Start <= now)
        {
            throw new DomainException("An appointment must start in the future.");
        }

        return new Appointment
        {
            Id = Guid.CreateVersion7(),
            ProviderId = providerId,
            ServiceId = serviceId,
            CustomerName = NormalizeName(customerName),
            CustomerEmail = NormalizeEmail(customerEmail),
            StartsAt = range.Start,
            EndsAt = range.End,
            Status = AppointmentStatus.Booked,
            CreatedAt = now
        };
    }

    public void Cancel(DateTimeOffset now)
    {
        RequireBooked("cancelled");

        if (now >= StartsAt)
        {
            throw new DomainException("An appointment cannot be cancelled once it has started.");
        }

        Status = AppointmentStatus.Cancelled;
        CancelledAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        RequireBooked("completed");
        RequireStarted(now, "completed");

        Status = AppointmentStatus.Completed;
    }

    public void MarkNoShow(DateTimeOffset now)
    {
        RequireBooked("marked as a no-show");
        RequireStarted(now, "marked as a no-show");

        Status = AppointmentStatus.NoShow;
    }

    private void RequireBooked(string action)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainException($"Only booked appointments can be {action} (current status: {Status}).");
        }
    }

    private void RequireStarted(DateTimeOffset now, string action)
    {
        if (now < StartsAt)
        {
            throw new DomainException($"An appointment cannot be {action} before it starts.");
        }
    }

    private static string NormalizeName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;

        if (value.Length is 0 or > MaxNameLength)
        {
            throw new DomainException($"The customer name is required and can have at most {MaxNameLength} characters.");
        }

        return value;
    }

    private static string NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (value.Length is 0 or > MaxEmailLength
            || !MailAddress.TryCreate(value, out var parsed)
            || parsed.Address != value)
        {
            throw new DomainException("A valid customer email is required.");
        }

        return value;
    }
}
