using Bookit.Domain.Businesses;

namespace Bookit.Application.Catalog;

// Each AddAsync saves immediately: creating a business, a service or a provider is a single-step operation.

public interface IBusinessRepository
{
    Task AddAsync(Business business, CancellationToken cancellationToken);

    Task<Business?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Business>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
}

public interface IServiceRepository
{
    Task AddAsync(Service service, CancellationToken cancellationToken);

    Task<Service?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Service>> ListAsync(Guid businessId, CancellationToken cancellationToken);
}

public interface IProviderRepository
{
    Task AddAsync(Provider provider, CancellationToken cancellationToken);

    Task<Provider?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Provider>> ListAsync(Guid businessId, CancellationToken cancellationToken);
}