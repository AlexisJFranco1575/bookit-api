#pragma warning disable xUnit1051

using Bookit.Application.Catalog;
using Bookit.Application.Common;
using Bookit.Domain.Common;
using Xunit;

namespace Bookit.Application.Tests;

public class CatalogServiceTests
{
    private readonly FakeBusinessRepository _businesses = new();
    private readonly FakeServiceRepository _services = new();
    private readonly FakeProviderRepository _providers = new();
    private readonly CatalogService _catalog;

    public CatalogServiceTests()
    {
        _catalog = new CatalogService(
            _businesses,
            _services,
            _providers,
            new FixedTimeProvider(BookingWorld.Now));
    }

    private Task<BusinessResponse> CreateBusinessAsync() =>
        _catalog.CreateBusinessAsync(
            new CreateBusinessRequest("Barber Shop", "America/Guayaquil"),
            CancellationToken.None);

    [Fact]
    public async Task Creates_a_business()
    {
        var response = await CreateBusinessAsync();

        Assert.Equal("Barber Shop", response.Name);
        Assert.Equal("America/Guayaquil", response.TimeZoneId);
        Assert.Single(_businesses.Items);
    }

    [Fact]
    public async Task Rejects_an_unknown_time_zone()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _catalog.CreateBusinessAsync(
                new CreateBusinessRequest("Barber Shop", "Mars/Olympus"),
                CancellationToken.None));

        Assert.Empty(_businesses.Items);
    }

    [Fact]
    public async Task Adding_a_service_to_an_unknown_business_fails_with_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _catalog.AddServiceAsync(
                Guid.NewGuid(),
                new CreateServiceRequest("Haircut", 30),
                CancellationToken.None));
    }

    [Fact]
    public async Task Adds_a_provider_with_parsed_working_hours()
    {
        var business = await CreateBusinessAsync();

        var response = await _catalog.AddProviderAsync(
            business.Id,
            new CreateProviderRequest("Carlos", [new WorkingPeriodDto(DayOfWeek.Monday, "09:00", "12:00")]),
            CancellationToken.None);

        Assert.Single(response.WorkingPeriods);
        Assert.Equal("09:00", response.WorkingPeriods[0].Start);
        Assert.Equal(new TimeOnly(9, 0), _providers.Items[0].WorkingPeriods[0].Start);
    }

    [Fact]
    public async Task Rejects_a_working_hour_that_is_not_in_hh_mm_format()
    {
        var business = await CreateBusinessAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            _catalog.AddProviderAsync(
                business.Id,
                new CreateProviderRequest("Carlos", [new WorkingPeriodDto(DayOfWeek.Monday, "9am", "12:00")]),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_overlapping_working_periods()
    {
        var business = await CreateBusinessAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            _catalog.AddProviderAsync(
                business.Id,
                new CreateProviderRequest(
                    "Carlos",
                    [
                        new WorkingPeriodDto(DayOfWeek.Monday, "09:00", "13:00"),
                        new WorkingPeriodDto(DayOfWeek.Monday, "12:00", "17:00")
                    ]),
                CancellationToken.None));

        Assert.Empty(_providers.Items);
    }
}
