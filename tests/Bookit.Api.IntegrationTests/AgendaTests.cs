#pragma warning disable xUnit1051

using System.Globalization;
using System.Net;
using Bookit.Application.Agenda;
using Bookit.Domain.Appointments;
using Xunit;

namespace Bookit.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AgendaTests(ApiFactory factory)
{
    private static string AgendaUrl(Guid businessId, DateOnly date)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"/api/businesses/{businessId}/appointments?from={day}&to={day}";
    }

    [Fact]
    public async Task The_owner_sees_the_appointments_customers_booked()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();
        var customer = factory.CreateClient();
        var monday = TestHelpers.NextMonday();

        var availability = await customer.GetAvailabilityAsync(shop, monday);
        var booked = await customer.BookAsync(shop, availability.Slots[0], "ana@example.com");
        await booked.ShouldBeAsync(HttpStatusCode.Created);

        var response = await owner.GetAsync(AgendaUrl(shop.BusinessId, monday));
        await response.ShouldBeAsync(HttpStatusCode.OK);

        var item = Assert.Single(await response.ReadAsync<IReadOnlyList<AgendaItemResponse>>());
        Assert.Equal("ana@example.com", item.CustomerEmail);
        Assert.Equal("Ana Perez", item.CustomerName);
        Assert.Equal("Haircut", item.ServiceName);
        Assert.Equal("Carlos", item.ProviderName);
        Assert.Equal(9, item.LocalStartsAt.Hour);
        Assert.Equal(AppointmentStatus.Booked, item.Status);
    }

    [Fact]
    public async Task Another_owner_cannot_read_my_agenda()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();
        var intruder = await factory.CreateOwnerClientAsync();

        var response = await intruder.GetAsync(AgendaUrl(shop.BusinessId, TestHelpers.NextMonday()));

        await response.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_agenda_requires_signing_in()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();

        var response = await factory.CreateClient().GetAsync(AgendaUrl(shop.BusinessId, TestHelpers.NextMonday()));

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }
}