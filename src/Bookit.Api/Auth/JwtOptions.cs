namespace Bookit.Api.Auth;

/// <summary>Settings for the access tokens. The signing key must come from a secret, never from a committed file.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "bookit-api";

    public string Audience { get; set; } = "bookit-clients";

    /// <summary>At least 32 bytes of random data.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}