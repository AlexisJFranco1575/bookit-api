using Bookit.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class NotificationDataReader(BookitDbContext db) : INotificationDataReader
{
    public async Task<NotificationData?> GetAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var data = await (
            from appointment in db.Appointments.AsNoTracking()
            join provider in db.Providers.AsNoTracking() on appointment.ProviderId equals provider.Id
            join service in db.Services.AsNoTracking() on appointment.ServiceId equals service.Id
            join business in db.Businesses.AsNoTracking() on provider.BusinessId equals business.Id
            where appointment.Id == appointmentId
            select new NotificationData(
                appointment.Id,
                appointment.Status,
                appointment.CustomerName,
                appointment.CustomerEmail,
                appointment.StartsAt,
                appointment.EndsAt,
                service.Name,
                provider.Name,
                business.Name,
                business.TimeZoneId))
            .FirstOrDefaultAsync(cancellationToken);

        return data is null
            ? null
            : data with { StartsAt = data.StartsAt.ToUniversalTime(), EndsAt = data.EndsAt.ToUniversalTime() };
    }
}