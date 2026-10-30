using System.Security.Claims;
using Bookit.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookit.Api.Auth;

public static class UserClaims
{
    /// <summary>The id of the signed-in user, taken from the token's "sub" claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(value, out var id) ? id : throw new InvalidCredentialsException();
    }
}