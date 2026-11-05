using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bookit.Api.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBookitAuthentication(this IServiceCollection services)
    {
        // The app refuses to start with a missing or weak key, instead of failing later on the first login.
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
                "Jwt:SigningKey is required and must be at least 32 bytes long.")
            .Validate(
                options => options.ExpiryMinutes is > 0 and <= 1440,
                "Jwt:ExpiryMinutes must be between 1 and 1440.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer) && !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .ValidateOnStart();

        services.AddSingleton<Bookit.Application.Auth.ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured through options (not read eagerly) so the settings are the ones the host really uses.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Keep claim names as issued ("sub") instead of renaming them to long Microsoft URIs.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}