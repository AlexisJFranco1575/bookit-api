using Bookit.Application.Catalog;
using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class ProviderRepository(BookitDbContext db) : IProviderRepository
{
    public async Task AddAsync(Provider provider, CancellationToken cancellationToken)
    {
        db.Providers.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
    }

    // The working periods are owned by the provider, so EF loads them together with it.
    public Task<Provider?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Providers.FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Provider>> ListAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var items = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.BusinessId == businessId)
            .OrderBy(provider => provider.Name)
            .ToListAsync(cancellationToken);

        return items;
    }
}
