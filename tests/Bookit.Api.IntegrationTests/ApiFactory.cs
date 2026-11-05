using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace Bookit.Api.IntegrationTests;

/// <summary>
/// Starts a throwaway SQL Server in Docker and the whole API in memory, configured to use it.
/// One instance is shared by every test class (see ApiCollection), so the container starts only once.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-at-least-32-bytes-long";

    private readonly MsSqlContainer _database =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync() => await _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Added last, so these values win over appsettings files and the developer's user secrets.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Bookit"] = _database.GetConnectionString(),
                ["Jwt:SigningKey"] = SigningKey,
                ["Database:MigrateOnStartup"] = "true",
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["RateLimiting:BookingPermitLimit"] = "1000"
            }));
    }
}