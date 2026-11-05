using Bookit.Application.Catalog;
using Bookit.Application.Common;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Agenda;

/// <summary>Lets the owner of a business read its appointments for a range of local days.</summary>
public sealed class AgendaService(IBusinessRepository businesses, IAgendaReader agenda)
{
    public const int MaxDays = 62;

    public async Task<IReadOnlyList<AgendaItemResponse>> GetAgendaAsync(
        Guid ownerId,
        Guid businessId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var business = await businesses.GetAsync(businessId, cancellationToken);

        // Someone else's business looks exactly like one that does not exist.
        if (business is null || !business.IsOwnedBy(ownerId))
        {
            throw new NotFoundException("Business", businessId);
        }

        if (toDate < fromDate)
        {
            throw new DomainException("The end date cannot be before the start date.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > MaxDays)
        {
            throw new DomainException($"The agenda can cover at most {MaxDays} days at a time.");
        }

        // The days are local days of the business: "Monday" starts and ends at local midnight.
        var zone = business.TimeZone;
        var window = new TimeRange(StartOfDayUtc(fromDate, zone), StartOfDayUtc(toDate.AddDays(1), zone));

        var rows = await agenda.ListAsync(businessId, window, cancellationToken);

        return rows
            .Select(row => new AgendaItemResponse(
                row.Id,
                row.ProviderId,
                row.ProviderName,
                row.ServiceId,
                row.ServiceName,
                row.CustomerName,
                row.CustomerEmail,
                row.StartsAt,
                row.EndsAt,
                TimeZoneInfo.ConvertTime(row.StartsAt, zone),
                row.Status))
            .ToList();
    }

    private static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}