using System.Globalization;
using Bookit.Domain.Businesses;

namespace Bookit.Application.Catalog;

public sealed record CreateBusinessRequest(string? Name, string? TimeZoneId);

public sealed record BusinessResponse(Guid Id, string Name, string TimeZoneId, DateTimeOffset CreatedAt)
{
    public static BusinessResponse From(Business business) =>
        new(business.Id, business.Name, business.TimeZoneId, business.CreatedAt);
}

public sealed record CreateServiceRequest(string? Name, int DurationMinutes);

public sealed record ServiceResponse(Guid Id, Guid BusinessId, string Name, int DurationMinutes)
{
    public static ServiceResponse From(Service service) =>
        new(service.Id, service.BusinessId, service.Name, service.DurationMinutes);
}

/// <summary>A working period as it travels over the wire: times are "HH:mm" in the business's local time.</summary>
public sealed record WorkingPeriodDto(DayOfWeek Day, string Start, string End);

public sealed record CreateProviderRequest(string? Name, IReadOnlyList<WorkingPeriodDto>? WorkingPeriods);

public sealed record ProviderResponse(
    Guid Id,
    Guid BusinessId,
    string Name,
    IReadOnlyList<WorkingPeriodDto> WorkingPeriods)
{
    public static ProviderResponse From(Provider provider) =>
        new(
            provider.Id,
            provider.BusinessId,
            provider.Name,
            provider.WorkingPeriods
                .Select(period => new WorkingPeriodDto(
                    period.Day,
                    period.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                    period.End.ToString("HH:mm", CultureInfo.InvariantCulture)))
                .ToList());
}
