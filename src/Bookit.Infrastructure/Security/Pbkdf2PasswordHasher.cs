using System.Security.Cryptography;
using Bookit.Application.Auth;

namespace Bookit.Infrastructure.Security;

/// <summary>
/// Hashes passwords with PBKDF2 (HMAC-SHA256), a random salt per password and 600,000 iterations
/// (the OWASP recommendation for this algorithm). The stored text is
/// "PBKDF2-SHA256$iterations$salt$hash", so the cost can be raised later without breaking old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;
    private const int MaxAcceptedIterations = 10_000_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string passwordHash, string password)
    {
        var parts = passwordHash.Split('$');

        if (parts.Length != 4
            || parts[0] != Prefix
            || !int.TryParse(parts[1], out var iterations)
            || iterations is < 1 or > MaxAcceptedIterations)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length < 8 || expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Constant-time comparison: the time taken does not reveal how many bytes matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}