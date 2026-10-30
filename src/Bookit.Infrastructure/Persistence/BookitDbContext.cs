using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Bookit.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class BookitDbContext(DbContextOptions<BookitDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Provider> Providers => Set<Provider>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookitDbContext).Assembly);
}