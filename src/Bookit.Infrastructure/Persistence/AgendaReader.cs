using Bookit.Application.Agenda;
using Bookit.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class AgendaReader(BookitDbContext db) : IAgendaReader
{
    public async Task<IReadOnlyList<AgendaRow>> ListAsync(
        Guid businessId,
        TimeRange window,
        CancellationToken cancellationToken)
    {
        var windowStart = window.Start;
        var windowEnd = window.End;

        // An appointment belongs to a business through its provider, so the query joins through providers.
        var rows = await (
            from appointment in db.Appointments.AsNoTracking()
            join provider in db.Providers.AsNoTracking() on appointment.ProviderId equals provider.Id
            join service in db.Services.AsNoTracking() on appointment.ServiceId equals service.Id
            where provider.BusinessId == businessId
                  && appointment.StartsAt >= windowStart
                  && appointment.StartsAt < windowEnd
            orderby appointment.StartsAt
            select new AgendaRow(
                appointment.Id,
                provider.Id,
                provider.Name,
                service.Id,
                service.Name,
                appointment.CustomerName,
                appointment.CustomerEmail,
                appointment.StartsAt,
                appointment.EndsAt,
                appointment.Status))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => row with
            {
                StartsAt = row.StartsAt.ToUniversalTime(),
                EndsAt = row.EndsAt.ToUniversalTime()
            })
            .ToList();
    }
}