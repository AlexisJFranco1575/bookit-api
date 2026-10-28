using Bookit.Domain.Common;

namespace Bookit.Domain.Businesses;

/// <summary>Something a business sells by appointment, for example a 30-minute haircut.</summary>
public sealed class Service
{
    public const int MaxNameLength = 100;
    public const int MinDurationMinutes = 5;
    public const int MaxDurationMinutes = 480;

    private Service()
    {
    }

    public Guid Id { get; private set; }

    public Guid BusinessId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DurationMinutes { get; private set; }

    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);

    public static Service Create(Guid businessId, string? name, int durationMinutes)
    {
        if (businessId == Guid.Empty)
        {
            throw new DomainException("A business is required.");
        }

        var cleanName = name?.Trim() ?? string.Empty;

        if (cleanName.Length is 0 or > MaxNameLength)
        {
            throw new DomainException($"The service name is required and can have at most {MaxNameLength} characters.");
        }

        if (durationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
        {
            throw new DomainException(
                $"The duration must be between {MinDurationMinutes} and {MaxDurationMinutes} minutes.");
        }

        return new Service
        {
            Id = Guid.CreateVersion7(),
            BusinessId = businessId,
            Name = cleanName,
            DurationMinutes = durationMinutes
        };
    }
}
