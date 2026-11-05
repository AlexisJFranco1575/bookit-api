using Xunit;

namespace Bookit.Api.IntegrationTests;

/// <summary>Makes every test class marked with [Collection("api")] share one ApiFactory (one SQL Server container).</summary>
[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}