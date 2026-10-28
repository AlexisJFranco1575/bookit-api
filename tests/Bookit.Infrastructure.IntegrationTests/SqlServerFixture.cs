using Bookit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

/// <summary>Starts a throwaway SQL Server in Docker, applies the real migrations and shares it with the tests.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A new context, as a new web request would get: its own connection and its own change tracker.</summary>
    public BookitDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BookitDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options);
}
