using Bookit.Application.Booking;
using Bookit.Application.Catalog;
using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeBusinessRepository : IBusinessRepository
{
    public List<Business> Items { get; } = [];

    public Task AddAsync(Business business, CancellationToken cancellationToken)
    {
        Items.Add(business);
        return Task.CompletedTask;
    }

    public Task<Business?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(business => business.Id == id));
}

internal sealed class FakeServiceRepository : IServiceRepository
{
    public List<Service> Items { get; } = [];

    public Task AddAsync(Service service, CancellationToken cancellationToken)
    {
        Items.Add(service);
        return Task.CompletedTask;
    }

    public Task<Service?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(service => service.Id == id));

    public Task<IReadOnlyList<Service>> ListAsync(Guid businessId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Service> list = Items.Where(service => service.BusinessId == businessId).ToList();
        return Task.FromResult(list);
    }
}

internal sealed class FakeProviderRepository : IProviderRepository
{
    public List<Provider> Items { get; } = [];

    public Task AddAsync(Provider provider, CancellationToken cancellationToken)
    {
        Items.Add(provider);
        return Task.CompletedTask;
    }

    public Task<Provider?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(provider => provider.Id == id));

    public Task<IReadOnlyList<Provider>> ListAsync(Guid businessId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Provider> list = Items.Where(provider => provider.BusinessId == businessId).ToList();
        return Task.FromResult(list);
    }
}

internal sealed class FakeAppointmentRepository : IAppointmentRepository
{
    public List<Appointment> Items { get; } = [];

    /// <summary>Simulates losing the race: the repository finds the time taken even though it looked free.</summary>
    public bool ForceConflict { get; set; }

    public Task<IReadOnlyList<TimeRange>> GetBusyRangesAsync(
        Guid providerId,
        TimeRange window,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TimeRange> busy = Items
            .Where(appointment => appointment.ProviderId == providerId
                                  && appointment.Status == AppointmentStatus.Booked)
            .Select(appointment => appointment.Range)
            .ToList();

        return Task.FromResult(busy);
    }

    public Task<bool> TryAddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var taken = ForceConflict
                    || Items.Any(other => other.ProviderId == appointment.ProviderId
                                          && other.Status == AppointmentStatus.Booked
                                          && other.Range.Overlaps(appointment.Range));

        if (taken)
        {
            return Task.FromResult(false);
        }

        Items.Add(appointment);
        return Task.FromResult(true);
    }

    public Task<Appointment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(appointment => appointment.Id == id));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
