using Bookit.Application.Catalog;
using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class BusinessRepository(BookitDbContext db) : IBusinessRepository
{
    public async Task AddAsync(Business business, CancellationToken cancellationToken)
    {
        db.Businesses.Add(business);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Business?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Businesses.FirstOrDefaultAsync(business => business.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Business>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var items = await db.Businesses
            .AsNoTracking()
            .Where(business => business.OwnerId == ownerId)
            .OrderBy(business => business.Name)
            .ToListAsync(cancellationToken);

        return items;
    }
}