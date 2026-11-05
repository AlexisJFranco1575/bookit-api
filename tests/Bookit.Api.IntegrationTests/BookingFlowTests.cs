#pragma warning disable xUnit1051

using System.Net;
using Bookit.Application.Booking;
using Bookit.Application.Catalog;
using Bookit.Domain.Appointments;
using Xunit;

namespace Bookit.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task A_customer_books_and_cancels_without_an_account()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();
        var customer = factory.CreateClient(); // no token: customers do not sign in
        var monday = TestHelpers.NextMonday();

        // 09:00-17:00 with a 30-minute service every 15 minutes: starts from 09:00 to 16:30 = 31 slots.
        var before = await customer.GetAvailabilityAsync(shop, monday);
        Assert.Equal(31, before.Slots.Count);
        Assert.Equal(9, before.Slots[0].LocalStartsAt.Hour);

        var booked = await customer.BookAsync(shop, before.Slots[0], "ana@example.com");
        await booked.ShouldBeAsync(HttpStatusCode.Created);
        Assert.NotNull(booked.Headers.Location);
        var appointment = await booked.ReadAsync<AppointmentResponse>();
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);

        // The booked slot and the one that overlaps it are gone.
        var during = await customer.GetAvailabilityAsync(shop, monday);
        Assert.Equal(29, during.Slots.Count);

        var again = await customer.BookAsync(shop, before.Slots[0], "bob@example.com");
        await again.ShouldBeAsync(HttpStatusCode.Conflict);

        var fetched = await customer.GetAsync($"/api/appointments/{appointment.Id}");
        await fetched.ShouldBeAsync(HttpStatusCode.OK);

        var cancelled = await customer.PostAsync($"/api/appointments/{appointment.Id}/cancel", null);
        await cancelled.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(AppointmentStatus.Cancelled, (await cancelled.ReadAsync<AppointmentResponse>()).Status);

        var after = await customer.GetAvailabilityAsync(shop, monday);
        Assert.Equal(31, after.Slots.Count);
    }

    [Fact]
    public async Task Customers_can_read_the_catalog_without_signing_in()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();
        var customer = factory.CreateClient();

        var business = await customer.GetAsync($"/api/businesses/{shop.BusinessId}");
        await business.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("Barber Shop", (await business.ReadAsync<BusinessResponse>()).Name);

        var services = await customer.GetAsync($"/api/businesses/{shop.BusinessId}/services");
        await services.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("Haircut", Assert.Single(await services.ReadAsync<IReadOnlyList<ServiceResponse>>()).Name);

        var providers = await customer.GetAsync($"/api/businesses/{shop.BusinessId}/providers");
        await providers.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("Carlos", Assert.Single(await providers.ReadAsync<IReadOnlyList<ProviderResponse>>()).Name);
    }
}