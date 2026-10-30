using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookit.Api;

/// <summary>Per-client request limits for the endpoints anyone can call. Limits are per minute and configurable.</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Booking = "booking";

    public static IServiceCollection AddBookitRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Auth, context => PerClient(context, "RateLimiting:AuthPermitLimit", 10));
            options.AddPolicy(Booking, context => PerClient(context, "RateLimiting:BookingPermitLimit", 20));
        });

        return services;
    }

    private static RateLimitPartition<string> PerClient(HttpContext context, string settingKey, int defaultLimit)
    {
        var limit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue(settingKey, defaultLimit);
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            client,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    }
}