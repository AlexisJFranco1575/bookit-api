using Bookit.Application.Catalog;
using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class ServiceRepository(BookitDbContext db) : IServiceRepository
{
    public async Task AddAsync(Service service, CancellationToken cancellationToken)
    {
        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Service?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Services.FirstOrDefaultAsync(service => service.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Service>> ListAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var items = await db.Services
            .AsNoTracking()
            .Where(service => service.BusinessId == businessId)
            .OrderBy(service => service.Name)
            .ToListAsync(cancellationToken);

        return items;
    }
}
