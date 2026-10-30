#pragma warning disable xUnit1051

using System.Net;
using Bookit.Application.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bookit.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ConcurrencyAndLimitsTests(ApiFactory factory)
{
    [Fact]
    public async Task Twenty_customers_booking_the_same_slot_over_http_produce_one_201_and_nineteen_409()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();
        var availability = await factory.CreateClient().GetAvailabilityAsync(shop, TestHelpers.NextMonday());
        var slot = availability.Slots[0];

        var statuses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(async i =>
            {
                using var customer = factory.CreateClient();
                var response = await customer.BookAsync(shop, slot, $"customer{i}@example.com");

                return response.StatusCode;
            }));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(19, statuses.Count(status => status == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Too_many_login_attempts_from_one_client_get_a_429()
    {
        // A second copy of the API, with a limit of 3 attempts per minute, on the same database.
        using var limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "3" })));

        var client = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostJsonAsync(
                "/api/auth/login",
                new LoginRequest("nobody@example.com", "Wrong_Passw0rd!"));

            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            new[]
            {
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests,
                HttpStatusCode.TooManyRequests
            },
            statuses);
    }
}