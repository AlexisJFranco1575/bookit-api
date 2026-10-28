using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bookit.Infrastructure.Persistence;

/// <summary>
/// Lets the EF tools create migrations without running the application. Creating a migration never
/// connects to the database, so the fallback connection string below does not need to be reachable.
/// </summary>
public sealed class BookitDbContextFactory : IDesignTimeDbContextFactory<BookitDbContext>
{
    public BookitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BOOKIT_CONNECTION")
            ?? "Server=localhost,1433;Database=Bookit;User Id=sa;Password=Design_Time_Only_1!;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<BookitDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BookitDbContext(options);
    }
}
