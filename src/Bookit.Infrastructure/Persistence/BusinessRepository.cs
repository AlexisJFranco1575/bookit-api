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
}
