#pragma warning disable xUnit1051

using System.Net;
using System.Text;
using Bookit.Application.Auth;
using Bookit.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bookit.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ErrorMappingTests(ApiFactory factory)
{
    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        await response.ShouldBeAsync(expected);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        return await response.ReadAsync<ProblemDetails>();
    }

    private async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Ana Perez", TestHelpers.Password));

        await response.ShouldBeAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_a_409_whatever_its_case()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail();
        await RegisterAsync(client, email);

        var second = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email.ToUpperInvariant(), "Ana Perez", TestHelpers.Password));

        var problem = await ReadProblemAsync(second, HttpStatusCode.Conflict);
        Assert.Equal(409, problem.Status);
    }

    [Fact]
    public async Task A_weak_password_is_a_422()
    {
        var response = await factory.CreateClient().PostJsonAsync(
            "/api/auth/register",
            new RegisterRequest(TestHelpers.UniqueEmail(), "Ana Perez", "short"));

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(422, problem.Status);
    }

    [Fact]
    public async Task An_invalid_email_is_a_422()
    {
        var response = await factory.CreateClient().PostJsonAsync(
            "/api/auth/register",
            new RegisterRequest("not-an-email", "Ana Perez", TestHelpers.Password));

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_401()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail();
        await RegisterAsync(client, email);

        var wrongPassword = await ReadProblemAsync(
            await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, "Wrong_Passw0rd!")),
            HttpStatusCode.Unauthorized);

        var unknownEmail = await ReadProblemAsync(
            await client.PostJsonAsync("/api/auth/login", new LoginRequest(TestHelpers.UniqueEmail(), TestHelpers.Password)),
            HttpStatusCode.Unauthorized);

        // Nothing in the answer reveals which of the two accounts exist.
        Assert.Equal(wrongPassword.Detail, unknownEmail.Detail);
    }

    [Fact]
    public async Task An_unknown_business_is_a_404()
    {
        var response = await factory.CreateClient().GetAsync($"/api/businesses/{Guid.NewGuid()}");

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_a_400()
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/auth/login",
            new StringContent("{ this is not json", Encoding.UTF8, "application/json"));

        await ReadProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_day_of_the_week_sent_as_a_number_is_a_400()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var shop = await owner.CreateBookableShopAsync();

        var body = new StringContent(
            "{\"name\":\"Marta\",\"workingPeriods\":[{\"day\":1,\"start\":\"09:00\",\"end\":\"12:00\"}]}",
            Encoding.UTF8,
            "application/json");

        var response = await owner.PostAsync($"/api/businesses/{shop.BusinessId}/providers", body);

        await ReadProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_time_zone_is_a_422()
    {
        var owner = await factory.CreateOwnerClientAsync();

        var response = await owner.PostJsonAsync(
            "/api/businesses",
            new CreateBusinessRequest("Barber Shop", "Mars/Olympus"));

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Asking_for_availability_without_the_required_values_is_a_400()
    {
        var response = await factory.CreateClient().GetAsync("/api/availability");

        await ReadProblemAsync(response, HttpStatusCode.BadRequest);
    }
}