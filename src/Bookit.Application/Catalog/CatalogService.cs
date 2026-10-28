using System.Globalization;
using Bookit.Application.Common;
using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Catalog;

/// <summary>Sets up what can be booked: businesses, their services and their providers.</summary>
public sealed class CatalogService(
    IBusinessRepository businesses,
    IServiceRepository services,
    IProviderRepository providers,
    TimeProvider clock)
{
    public async Task<BusinessResponse> CreateBusinessAsync(
        CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var business = Business.Create(request.Name, request.TimeZoneId, clock.GetUtcNow());

        await businesses.AddAsync(business, cancellationToken);

        return BusinessResponse.From(business);
    }

    public async Task<BusinessResponse> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        BusinessResponse.From(await RequireBusinessAsync(businessId, cancellationToken));

    public async Task<ServiceResponse> AddServiceAsync(
        Guid businessId,
        CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        await RequireBusinessAsync(businessId, cancellationToken);

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
        Guid businessId,
        CreateProviderRequest request,
        CancellationToken cancellationToken)
    {
        await RequireBusinessAsync(businessId, cancellationToken);

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
