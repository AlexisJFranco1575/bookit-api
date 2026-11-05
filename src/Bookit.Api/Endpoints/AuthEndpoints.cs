using Bookit.Application.Auth;

namespace Bookit.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken cancellationToken) =>
        {
            var user = await auth.RegisterAsync(request, cancellationToken);

            return Results.Json(user, statusCode: StatusCodes.Status201Created);
        });

        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken cancellationToken) =>
            Results.Ok(await auth.LoginAsync(request, cancellationToken)));

        return app;
    }
}