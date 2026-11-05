#pragma warning disable xUnit1051

using System.Net;
using System.Net.Http.Headers;
using Bookit.Application.Catalog;
using Xunit;

namespace Bookit.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OwnershipTests(ApiFactory factory)
{
    [Fact]
    public async Task Another_owner_cannot_change_my_business_and_cannot_tell_it_exists()
    {
        var ownerA = await factory.CreateOwnerClientAsync();
        var shop = await ownerA.CreateBookableShopAsync();
        var ownerB = await factory.CreateOwnerClientAsync();

        var service = await ownerB.PostJsonAsync(
            $"/api/businesses/{shop.BusinessId}/services",
            new CreateServiceRequest("Massage", 60));
        await service.ShouldBeAsync(HttpStatusCode.NotFound);

        var provider = await ownerB.PostJsonAsync(
            $"/api/businesses/{shop.BusinessId}/providers",
            new CreateProviderRequest("Marta", [new WorkingPeriodDto(DayOfWeek.Tuesday, "09:00", "12:00")]));
        await provider.ShouldBeAsync(HttpStatusCode.NotFound);

        // The business is untouched: still one service.
        var services = await factory.CreateClient().GetAsync($"/api/businesses/{shop.BusinessId}/services");
        Assert.Single(await services.ReadAsync<IReadOnlyList<ServiceResponse>>());
    }

    [Fact]
    public async Task Each_owner_lists_only_their_own_businesses()
    {
        var ownerA = await factory.CreateOwnerClientAsync();
        await ownerA.CreateBookableShopAsync();
        var ownerB = await factory.CreateOwnerClientAsync();

        var mineA = await ownerA.GetAsync("/api/businesses/mine");
        await mineA.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Single(await mineA.ReadAsync<IReadOnlyList<BusinessResponse>>());

        var mineB = await ownerB.GetAsync("/api/businesses/mine");
        await mineB.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Empty(await mineB.ReadAsync<IReadOnlyList<BusinessResponse>>());
    }

    [Fact]
    public async Task Creating_a_business_requires_signing_in()
    {
        var response = await factory.CreateClient().PostJsonAsync(
            "/api/businesses",
            new CreateBusinessRequest("Barber Shop", "America/Guayaquil"));

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_made_up_token_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/businesses/mine");

        await response.ShouldBeAsync(HttpStatusCode.Unauthorized);
    }
}