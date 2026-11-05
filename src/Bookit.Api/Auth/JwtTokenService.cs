using System.Security.Claims;
using System.Text;
using Bookit.Application.Auth;
using Bookit.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bookit.Api.Auth;

/// <summary>Creates signed JWT access tokens (HMAC-SHA256) that carry the user's id in the "sub" claim.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName)
            }),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}