using Bookit.Application.Auth;
using Bookit.Application.Booking;
using Bookit.Application.Catalog;
using Bookit.Infrastructure.Persistence;
using Bookit.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Bookit.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the repositories and the password hasher. The DbContext itself is registered by the host,
    /// because only the host knows where the connection string comes from.
    /// </summary>
    public static IServiceCollection AddBookitInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        return services;
    }
}