#pragma warning disable xUnit1051

using System.Net;
using Xunit;

namespace Bookit.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthAndDocsTests(ApiFactory factory)
{
    [Fact]
    public async Task The_health_check_answers_without_signing_in()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        await response.ShouldBeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_openapi_document_is_generated_and_lists_the_booking_endpoints()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        await response.ShouldBeAsync(HttpStatusCode.OK);

        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/appointments", document);
        Assert.Contains("/api/availability", document);
    }
}