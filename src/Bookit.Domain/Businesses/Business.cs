using Bookit.Domain.Common;

namespace Bookit.Domain.Businesses;

/// <summary>A business that takes appointments (a barbershop, a clinic). Its time zone decides what "9:00" means.</summary>
public sealed class Business
{
    public const int MaxNameLength = 100;

    // Persistence needs a parameterless constructor; the factory below is the only public way in.
    private Business()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The user who manages this business.</summary>
    public Guid OwnerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>An IANA time zone id such as America/Guayaquil.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    public static Business Create(Guid ownerId, string? name, string? timeZoneId, DateTimeOffset now)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("A business needs an owner.");
        }

        var cleanName = name?.Trim() ?? string.Empty;

        if (cleanName.Length is 0 or > MaxNameLength)
        {
            throw new DomainException($"The business name is required and can have at most {MaxNameLength} characters.");
        }

        var zoneId = timeZoneId?.Trim() ?? string.Empty;

        if (zoneId.Length == 0 || !Exists(zoneId))
        {
            throw new DomainException("A valid IANA time zone is required, for example America/Guayaquil.");
        }

        return new Business
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Name = cleanName,
            TimeZoneId = zoneId,
            CreatedAt = now
        };
    }

    private static bool Exists(string zoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}