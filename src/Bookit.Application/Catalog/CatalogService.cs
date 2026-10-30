using System.Globalization;
using Bookit.Application.Common;
using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Catalog;

/// <summary>
/// Sets up what can be booked: businesses, their services and their providers.
/// Methods that change data take the owner's id; reading is public so customers can see the booking page.
/// </summary>
public sealed class CatalogService(
    IBusinessRepository businesses,
    IServiceRepository services,
    IProviderRepository providers,
    TimeProvider clock)
{
    public async Task<BusinessResponse> CreateBusinessAsync(
        Guid ownerId,
        CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var business = Business.Create(ownerId, request.Name, request.TimeZoneId, clock.GetUtcNow());

        await businesses.AddAsync(business, cancellationToken);

        return BusinessResponse.From(business);
    }

    public async Task<IReadOnlyList<BusinessResponse>> ListOwnedBusinessesAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var items = await businesses.ListByOwnerAsync(ownerId, cancellationToken);

        return items.Select(BusinessResponse.From).ToList();
    }

    public async Task<BusinessResponse> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        BusinessResponse.From(await RequireBusinessAsync(businessId, cancellationToken));

    public async Task<ServiceResponse> AddServiceAsync(
        Guid ownerId,
        Guid businessId,
        CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        await RequireOwnedBusinessAsync(ownerId, businessId, cancellationToken);

        var service = Service.Create(businessId, request.Name, request.DurationMinutes);

        await services.AddAsync(service, cancellationToken);

        return ServiceResponse.From(service);
    }

    public async Task<IReadOnlyList<ServiceResponse>> ListServicesAsync(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        await RequireBusinessAsync(businessId, cancellationToken);

        var items = await services.ListAsync(businessId, cancellationToken);

        return items.Select(ServiceResponse.From).ToList();
    }

    public async Task<ProviderResponse> AddProviderAsync(
        Guid ownerId,
        Guid businessId,
        CreateProviderRequest request,
        CancellationToken cancellationToken)
    {
        await RequireOwnedBusinessAsync(ownerId, businessId, cancellationToken);

        var schedule = new WeeklySchedule((request.WorkingPeriods ?? []).Select(ToPeriod));
        var provider = Provider.Create(businessId, request.Name, schedule);

        await providers.AddAsync(provider, cancellationToken);

        return ProviderResponse.From(provider);
    }

    public async Task<IReadOnlyList<ProviderResponse>> ListProvidersAsync(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        await RequireBusinessAsync(businessId, cancellationToken);

        var items = await providers.ListAsync(businessId, cancellationToken);

        return items.Select(ProviderResponse.From).ToList();
    }

    private async Task<Business> RequireBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        await businesses.GetAsync(businessId, cancellationToken)
        ?? throw new NotFoundException("Business", businessId);

    /// <summary>
    /// A business that belongs to someone else is reported as "not found", exactly like one that does not exist,
    /// so nobody can discover which business ids exist by trying them.
    /// </summary>
    private async Task<Business> RequireOwnedBusinessAsync(
        Guid ownerId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var business = await RequireBusinessAsync(businessId, cancellationToken);

        return business.IsOwnedBy(ownerId) ? business : throw new NotFoundException("Business", businessId);
    }

    private static WorkingPeriod ToPeriod(WorkingPeriodDto dto)
    {
        if (!Enum.IsDefined(dto.Day))
        {
            throw new DomainException("A working period has an invalid day of the week.");
        }

        return new WorkingPeriod(dto.Day, ParseTime(dto.Start), ParseTime(dto.End));
    }

    private static TimeOnly ParseTime(string? value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new DomainException($"'{value}' is not a valid time; use HH:mm, for example 09:00.");
}