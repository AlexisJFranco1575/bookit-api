#pragma warning disable xUnit1051

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookit.Application.Auth;
using Bookit.Application.Booking;
using Bookit.Application.Catalog;
using Xunit;

namespace Bookit.Api.IntegrationTests;

internal sealed record Shop(Guid BusinessId, Guid ServiceId, Guid ProviderId);

internal static class TestHelpers
{
    public const string Password = "Demo_Passw0rd!";

    /// <summary>The API sends and expects enums as text ("Monday"), so the test client must do the same.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string UniqueEmail() => $"owner-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);

        return value ?? throw new InvalidOperationException("The response body was empty.");
    }

    /// <summary>Fails the test showing the response body, which is far more useful than "expected 201 but was 422".</summary>
    public static async Task<HttpResponseMessage> ShouldBeAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        return response;
    }

    /// <summary>A Monday at least a week ahead, so it is always in the future and always bookable.</summary>
    public static DateOnly NextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;

        return today.AddDays(daysUntilMonday + 7);
    }

    /// <summary>A new owner: registers, signs in and returns a client that sends the token on every request.</summary>
    public static async Task<HttpClient> CreateOwnerClientAsync(this ApiFactory factory)
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();

        var registered = await client.PostJsonAsync("/api/auth/register", new RegisterRequest(email, "Owner Demo", Password));
        await registered.ShouldBeAsync(HttpStatusCode.Created);

        var loggedIn = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        await loggedIn.ShouldBeAsync(HttpStatusCode.OK);
        var token = await loggedIn.ReadAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return client;
    }

    /// <summary>A barbershop in Guayaquil with a 30-minute haircut and a barber who works Mondays 09:00-17:00.</summary>
    public static async Task<Shop> CreateBookableShopAsync(this HttpClient owner)
    {
        var businessResponse = await owner.PostJsonAsync(
            "/api/businesses",
            new CreateBusinessRequest("Barber Shop", "America/Guayaquil"));
        await businessResponse.ShouldBeAsync(HttpStatusCode.Created);
        var business = await businessResponse.ReadAsync<BusinessResponse>();

        var serviceResponse = await owner.PostJsonAsync(
            $"/api/businesses/{business.Id}/services",
            new CreateServiceRequest("Haircut", 30));
        await serviceResponse.ShouldBeAsync(HttpStatusCode.Created);
        var service = await serviceResponse.ReadAsync<ServiceResponse>();

        var providerResponse = await owner.PostJsonAsync(
            $"/api/businesses/{business.Id}/providers",
            new CreateProviderRequest("Carlos", [new WorkingPeriodDto(DayOfWeek.Monday, "09:00", "17:00")]));
        await providerResponse.ShouldBeAsync(HttpStatusCode.Created);
        var provider = await providerResponse.ReadAsync<ProviderResponse>();

        return new Shop(business.Id, service.Id, provider.Id);
    }

    public static async Task<AvailabilityResponse> GetAvailabilityAsync(this HttpClient client, Shop shop, DateOnly date)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var response = await client.GetAsync(
            $"/api/availability?providerId={shop.ProviderId}&serviceId={shop.ServiceId}&from={day}&to={day}");
        await response.ShouldBeAsync(HttpStatusCode.OK);

        return await response.ReadAsync<AvailabilityResponse>();
    }

    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, Shop shop, SlotResponse slot, string customerEmail) =>
        client.PostJsonAsync(
            "/api/appointments",
            new BookAppointmentRequest(shop.ProviderId, shop.ServiceId, slot.StartsAt, "Ana Perez", customerEmail));
}